using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sentium.Sandbox.Artifacts;
using Sentium.Sandbox.Options;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Sandbox;

public sealed class ArtifactServiceTests : IDisposable
{
    private readonly string _jobDirectory = Directory.CreateTempSubdirectory("sentium-artifacts-").FullName;
    private readonly string _outside = Path.Combine(Path.GetTempPath(), $"sentium-secret-{Guid.NewGuid():N}.txt");
    private readonly BlobContainerClient _container = Substitute.For<BlobContainerClient>();
    private readonly List<string> _uploaded = [];
    private readonly ArtifactService _sut;

    public ArtifactServiceTests()
    {
        var blobs = Substitute.For<BlobServiceClient>();
        blobs.GetBlobContainerClient(Arg.Any<string>()).Returns(_container);
        _container.GetBlobClient(Arg.Any<string>()).Returns(call => FakeBlob(call.Arg<string>()));

        _sut = new ArtifactService(blobs, Options.Create(new SandboxOptions()), NullLogger<ArtifactService>.Instance);
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

    private BlobClient FakeBlob(string name)
    {
        var blob = Substitute.For<BlobClient>();
        blob.Uri.Returns(new Uri($"http://blobs.test/artifacts/{name}"));
        blob.UploadAsync(Arg.Any<Stream>(), Arg.Any<BlobUploadOptions>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                _uploaded.Add(name);
                return Task.FromResult(Substitute.For<Response<BlobContentInfo>>());
            });
        return blob;
    }
}
