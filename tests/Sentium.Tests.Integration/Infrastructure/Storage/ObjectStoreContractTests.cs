using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Sentium.Infrastructure.Storage;
using Xunit;

namespace Sentium.Tests.Integration.Infrastructure.Storage;

/// <summary>
/// What the modules rely on from <see cref="IObjectStore"/>, against the object store server the deployments run.
/// Re-run after changing or upgrading that server, or AWSSDK.S3.
/// </summary>
public sealed class ObjectStoreContractTests(ObjectStoreServerFixture server) : IClassFixture<ObjectStoreServerFixture>
{
    private readonly IObjectStore _store = server.Store;
    private readonly string _bucket = $"contract-{Guid.NewGuid():N}"[..30];

    [Fact]
    public async Task Put_ThenOpenRead_KeepsContentTypeLengthAndAnyMetadataText()
    {
        var ct = TestContext.Current.CancellationToken;
        var metadata = new Dictionary<string, string> { ["FileName"] = "Résumé 100% (final).pdf", ["WorkspaceId"] = "42" };

        await _store.PutAsync(_bucket, "dir/file name.pdf", new MemoryStream("pdf-bytes"u8.ToArray()), "application/pdf", metadata, ct);
        var stored = await _store.OpenReadAsync(_bucket, "dir/file name.pdf", ct);

        Assert.NotNull(stored);
        await using var content = stored!.Content;
        Assert.Equal("application/pdf", stored.ContentType);
        Assert.Equal(9, stored.Length);
        Assert.Equal("Résumé 100% (final).pdf", Assert.Contains("filename", stored.Metadata));
        Assert.Equal("42", Assert.Contains("workspaceid", stored.Metadata));
        Assert.Equal("pdf-bytes", await ReadAllAsync(content, ct));
    }

    [Fact]
    public async Task OpenRead_IsSeekable_SoRangeRequestsWork()
    {
        var ct = TestContext.Current.CancellationToken;
        var body = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Range(0, 2000).Select(i => (char)('a' + (i % 26)))));
        await _store.PutAsync(_bucket, "range.txt", new MemoryStream(body), "text/plain", ct: ct);

        await using var content = (await _store.OpenReadAsync(_bucket, "range.txt", ct))!.Content;

        Assert.True(content.CanSeek);
        Assert.Equal(body.Length, content.Length);

        content.Seek(1500, SeekOrigin.Begin);
        var tail = new byte[10];
        await content.ReadExactlyAsync(tail, ct);
        Assert.Equal(body[1500..1510], tail);

        content.Position = 3;
        var head = new byte[4];
        await content.ReadExactlyAsync(head, ct);
        Assert.Equal(body[3..7], head);

        content.Seek(-5, SeekOrigin.End);
        Assert.Equal(Encoding.ASCII.GetString(body[^5..]), await ReadAllAsync(content, ct));
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

        await Assert.ThrowsAnyAsync<Exception>(read);
    }

    [Fact]
    public async Task Put_AcceptsNonSeekableAndEmptyStreams()
    {
        var ct = TestContext.Current.CancellationToken;

        await using var source = new NonSeekableStream("streamed"u8.ToArray());
        await _store.PutAsync(_bucket, "piped.bin", source, ct: ct);
        await _store.PutAsync(_bucket, "empty.bin", new MemoryStream(), ct: ct);

        await using var piped = (await _store.OpenReadAsync(_bucket, "piped.bin", ct))!.Content;
        Assert.Equal("streamed", await ReadAllAsync(piped, ct));
        var empty = await _store.OpenReadAsync(_bucket, "empty.bin", ct);
        Assert.Equal(0, empty!.Length);
        await using var emptyContent = empty.Content;
        Assert.Empty(await ReadAllAsync(emptyContent, ct));
        Assert.Equal("application/octet-stream", empty.ContentType);
    }

    [Fact]
    public async Task MissingObjectsAndBuckets_AreNotErrors()
    {
        var ct = TestContext.Current.CancellationToken;
        var missingBucket = $"missing-{Guid.NewGuid():N}"[..30];

        Assert.Null(await _store.OpenReadAsync(missingBucket, "nothing", ct));
        Assert.Empty(await _store.ListKeysAsync(missingBucket, ct: ct).ToListAsync(ct));
        await _store.DeleteAsync(missingBucket, "nothing", ct);
        await _store.DeletePrefixAsync(missingBucket, "x/", ct);

        await _store.PutAsync(_bucket, "present", new MemoryStream("x"u8.ToArray()), ct: ct);
        Assert.Null(await _store.OpenReadAsync(_bucket, "absent", ct));
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

        Assert.Equal(count + 1, (await _store.ListKeysAsync(_bucket, ct: ct).ToListAsync(ct)).Count);
        Assert.Contains(_bucket, await _store.ListBucketsAsync(ct).ToListAsync(ct));

        await _store.DeletePrefixAsync(_bucket, "asset-a/", ct);

        Assert.Equal(new string[] { "asset-b/keep" }, await _store.ListKeysAsync(_bucket, ct: ct).ToListAsync(ct));
    }

    [Fact]
    public async Task WrongCredentials_FailThePing_AndTheHealthCheck()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = server.BuildHost(server.ConnectionString(secretKey: "wrong-secret"));

        var ping = () => host.Services.GetRequiredService<IObjectStore>().PingAsync(ct);
        var report = await host.Services.GetRequiredService<HealthCheckService>().CheckHealthAsync(ct);
        var goodReport = await server.Services.GetRequiredService<HealthCheckService>().CheckHealthAsync(ct);

        await Assert.ThrowsAnyAsync<Exception>(ping);
        Assert.Equal(HealthStatus.Unhealthy, report.Status);
        Assert.Equal(HealthStatus.Healthy, goodReport.Status);
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
