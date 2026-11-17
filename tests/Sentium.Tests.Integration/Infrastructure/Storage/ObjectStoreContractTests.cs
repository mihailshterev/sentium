using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Sentium.Infrastructure.Storage;
using Xunit;

namespace Sentium.Tests.Integration.Infrastructure.Storage;

/// <summary>
/// What the modules rely on from <see cref="IObjectStore"/>, against the SeaweedFS the deployments ship. Re-run after
/// upgrading SeaweedFS or AWSSDK.S3.
/// </summary>
public sealed class ObjectStoreContractTests(SeaweedFsFixture seaweed) : IClassFixture<SeaweedFsFixture>
{
    private readonly IObjectStore _store = seaweed.Store;
    private readonly string _bucket = $"contract-{Guid.NewGuid():N}"[..30];

    [Fact]
    public async Task Put_ThenOpenRead_KeepsContentTypeLengthAndAnyMetadataText()
    {
        var ct = TestContext.Current.CancellationToken;
        var metadata = new Dictionary<string, string> { ["FileName"] = "Résumé 100% (final).pdf", ["WorkspaceId"] = "42" };

        await _store.PutAsync(_bucket, "dir/file name.pdf", new MemoryStream("pdf-bytes"u8.ToArray()), "application/pdf", metadata, ct);
        var stored = await _store.OpenReadAsync(_bucket, "dir/file name.pdf", ct);

        stored.Should().NotBeNull();
        await using var content = stored!.Content;
        stored.ContentType.Should().Be("application/pdf");
        stored.Length.Should().Be(9);
        stored.Metadata.Should().Contain("filename", "Résumé 100% (final).pdf").And.Contain("workspaceid", "42");
        (await ReadAllAsync(content, ct)).Should().Be("pdf-bytes");
    }

    [Fact]
    public async Task OpenRead_IsSeekable_SoRangeRequestsWork()
    {
        var ct = TestContext.Current.CancellationToken;
        var body = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Range(0, 2000).Select(i => (char)('a' + (i % 26)))));
        await _store.PutAsync(_bucket, "range.txt", new MemoryStream(body), "text/plain", ct: ct);

        await using var content = (await _store.OpenReadAsync(_bucket, "range.txt", ct))!.Content;

        content.CanSeek.Should().BeTrue();
        content.Length.Should().Be(body.Length);

        content.Seek(1500, SeekOrigin.Begin);
        var tail = new byte[10];
        await content.ReadExactlyAsync(tail, ct);
        tail.Should().Equal(body[1500..1510]);

        content.Position = 3;
        var head = new byte[4];
        await content.ReadExactlyAsync(head, ct);
        head.Should().Equal(body[3..7]);

        content.Seek(-5, SeekOrigin.End);
        (await ReadAllAsync(content, ct)).Should().Be(Encoding.ASCII.GetString(body[^5..]));
    }

    [Fact]
    public async Task OpenRead_FailsInsteadOfSplicing_WhenTheObjectIsReplacedMidRead()
    {
        var ct = TestContext.Current.CancellationToken;
        await _store.PutAsync(_bucket, "swap.txt", new MemoryStream("first version"u8.ToArray()), ct: ct);
        await using var content = (await _store.OpenReadAsync(_bucket, "swap.txt", ct))!.Content;

        await _store.PutAsync(_bucket, "swap.txt", new MemoryStream("second version"u8.ToArray()), ct: ct);
        content.Seek(6, SeekOrigin.Begin);
        var read = async () => await content.ReadAsync(new byte[4], ct);

        await read.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task Put_AcceptsNonSeekableAndEmptyStreams()
    {
        var ct = TestContext.Current.CancellationToken;

        await using var source = new NonSeekableStream("streamed"u8.ToArray());
        await _store.PutAsync(_bucket, "piped.bin", source, ct: ct);
        await _store.PutAsync(_bucket, "empty.bin", new MemoryStream(), ct: ct);

        await using var piped = (await _store.OpenReadAsync(_bucket, "piped.bin", ct))!.Content;
        (await ReadAllAsync(piped, ct)).Should().Be("streamed");
        var empty = await _store.OpenReadAsync(_bucket, "empty.bin", ct);
        empty!.Length.Should().Be(0);
        await using var emptyContent = empty.Content;
        (await ReadAllAsync(emptyContent, ct)).Should().BeEmpty();
        empty.ContentType.Should().Be("application/octet-stream");
    }

    [Fact]
    public async Task MissingObjectsAndBuckets_AreNotErrors()
    {
        var ct = TestContext.Current.CancellationToken;
        var missingBucket = $"missing-{Guid.NewGuid():N}"[..30];

        (await _store.OpenReadAsync(missingBucket, "nothing", ct)).Should().BeNull();
        (await _store.ListKeysAsync(missingBucket, ct: ct).ToListAsync(ct)).Should().BeEmpty();
        await _store.DeleteAsync(missingBucket, "nothing", ct);
        await _store.DeletePrefixAsync(missingBucket, "x/", ct);

        await _store.PutAsync(_bucket, "present", new MemoryStream("x"u8.ToArray()), ct: ct);
        (await _store.OpenReadAsync(_bucket, "absent", ct)).Should().BeNull();
        await _store.DeleteAsync(_bucket, "absent", ct);
    }

    [Fact]
    public async Task ListAndDeletePrefix_CoverEveryPage_AndOnlyThePrefix()
    {
        var ct = TestContext.Current.CancellationToken;
        const int count = 1005; // more than one ListObjectsV2 page

        await Parallel.ForEachAsync(Enumerable.Range(0, count), new ParallelOptions { MaxDegreeOfParallelism = 16, CancellationToken = ct }, async (i, token) =>
            await _store.PutAsync(_bucket, $"asset-a/{i:D4}", new MemoryStream([1]), ct: token));
        await _store.PutAsync(_bucket, "asset-b/keep", new MemoryStream([1]), ct: ct);

        (await _store.ListKeysAsync(_bucket, ct: ct).ToListAsync(ct)).Should().HaveCount(count + 1);
        (await _store.ListBucketsAsync(ct).ToListAsync(ct)).Should().Contain(_bucket);

        await _store.DeletePrefixAsync(_bucket, "asset-a/", ct);

        (await _store.ListKeysAsync(_bucket, ct: ct).ToListAsync(ct)).Should().Equal("asset-b/keep");
    }

    [Fact]
    public async Task WrongCredentials_FailThePing_AndTheHealthCheck()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = seaweed.BuildHost(seaweed.ConnectionString(secretKey: "wrong-secret"));

        var ping = () => host.Services.GetRequiredService<IObjectStore>().PingAsync(ct);
        var report = await host.Services.GetRequiredService<HealthCheckService>().CheckHealthAsync(ct);
        var goodReport = await seaweed.Services.GetRequiredService<HealthCheckService>().CheckHealthAsync(ct);

        await ping.Should().ThrowAsync<Exception>();
        report.Status.Should().Be(HealthStatus.Unhealthy);
        goodReport.Status.Should().Be(HealthStatus.Healthy);
    }

    private static async Task<string> ReadAllAsync(Stream stream, CancellationToken ct)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        return await reader.ReadToEndAsync(ct);
    }

    private sealed class NonSeekableStream(byte[] data) : MemoryStream(data)
    {
        public override bool CanSeek => false;
    }
}
