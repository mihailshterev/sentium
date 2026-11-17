using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sentium.Infrastructure.Storage;
using Sentium.Sandbox.Artifacts;
using Sentium.Sandbox.Options;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Sandbox;

public sealed class ArtifactServiceTests : IDisposable
{
    private readonly string _jobDirectory = Directory.CreateTempSubdirectory("sentium-artifacts-").FullName;
    private readonly string _outside = Path.Combine(Path.GetTempPath(), $"sentium-secret-{Guid.NewGuid():N}.txt");
    private readonly IObjectStore _store = Substitute.For<IObjectStore>();
    private readonly List<string> _uploaded = [];
    private readonly ArtifactService _sut;

    public ArtifactServiceTests()
    {
        _store.PutAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string?>(), Arg.Any<IReadOnlyDictionary<string, string>?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                _uploaded.Add($"{call.ArgAt<string>(0)}/{call.ArgAt<string>(1)}");
                return Task.CompletedTask;
            });

        _sut = new ArtifactService(_store, Options.Create(new SandboxOptions()), NullLogger<ArtifactService>.Instance);
    }

    public void Dispose()
    {
        Directory.Delete(_jobDirectory, recursive: true);
        File.Delete(_outside);
    }

    [Fact]
    public async Task Harvest_SkipsInputsAndSymbolicLinks()
    {
        var ct = TestContext.Current.CancellationToken;
        await File.WriteAllTextAsync(_outside, "INTERNAL_API_KEY=secret", ct);
        await File.WriteAllTextAsync(Path.Combine(_jobDirectory, "main.py"), "print(1)", ct);
        await File.WriteAllTextAsync(Path.Combine(_jobDirectory, "result.csv"), "a,b", ct);

        try
        {
            File.CreateSymbolicLink(Path.Combine(_jobDirectory, "leak.txt"), _outside);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            Assert.Skip("This machine does not allow creating symbolic links.");
        }

        var artifacts = await _sut.HarvestAsync(_jobDirectory, new HashSet<string> { "main.py" }, Guid.NewGuid(), ct);

        artifacts.Select(a => a.FileName).Should().Equal("result.csv");
        _uploaded.Should().ContainSingle().Which.Should().EndWith("/result.csv");
    }

    [Fact]
    public async Task Harvest_ReferencesEachArtifactByBucketAndKey()
    {
        var ct = TestContext.Current.CancellationToken;
        var jobId = Guid.NewGuid();
        Directory.CreateDirectory(Path.Combine(_jobDirectory, "out"));
        await File.WriteAllTextAsync(Path.Combine(_jobDirectory, "out", "my chart.png"), "png", ct);

        var artifacts = await _sut.HarvestAsync(_jobDirectory, new HashSet<string>(), jobId, ct);

        var artifact = artifacts.Should().ContainSingle().Subject;
        artifact.Key.Should().Be($"{jobId:N}/out/my chart.png");
        artifact.MimeType.Should().Be("image/png");
        artifact.BlobUri.Should().Be(new Uri($"s3://sandbox-artifacts/{jobId:N}/out/my%20chart.png"));
        _uploaded.Should().Equal($"sandbox-artifacts/{jobId:N}/out/my chart.png");
    }
}
