using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sentium.Praxis.Features.Models;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis.Models;

public sealed class CatalogInstalledModelsTests : IDisposable
{
    private readonly IModelCatalog _catalog = Substitute.For<IModelCatalog>();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly CatalogInstalledModels _sut;

    public CatalogInstalledModelsTests()
    {
        _sut = new CatalogInstalledModels(_catalog, _cache, NullLogger<CatalogInstalledModels>.Instance);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _cache.Dispose();

    [Fact]
    public async Task InstalledModel_IsKept()
    {
        Declare("gemma-4-e4b", "qwen3-8b");

        Assert.Equal("qwen3-8b", await _sut.PickAsync(["qwen3-8b", "gemma-4-e4b"], Ct));
    }

    [Fact]
    public async Task RemovedModel_FallsBackToTheFirstInstalledDefault()
    {
        Declare("gemma-4-e4b");

        Assert.Equal("gemma-4-e4b", await _sut.PickAsync(["llama-3-removed", "also-removed", "gemma-4-e4b"], Ct));
    }

    [Fact]
    public async Task NothingInstalled_KeepsTheRequestedModel()
    {
        Declare("something-else");

        Assert.Equal("llama-3-removed", await _sut.PickAsync(["llama-3-removed", "gemma-4-e4b"], Ct));
    }

    [Fact]
    public async Task UnreachableServer_KeepsTheRequestedModel_AndAsksAgainNextTime()
    {
        _catalog.ListAsync(Arg.Any<CancellationToken>()).Returns((IReadOnlyList<CatalogModel>?)null);

        Assert.Equal("qwen3-8b", await _sut.PickAsync(["qwen3-8b", "gemma-4-e4b"], Ct));
        await _sut.PickAsync(["qwen3-8b", "gemma-4-e4b"], Ct);

        await _catalog.Received(2).ListAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SingleCandidate_NeedsNoLookup()
    {
        Assert.Equal("gemma-4-e4b", await _sut.PickAsync(["gemma-4-e4b"], Ct));

        await _catalog.DidNotReceive().ListAsync(Arg.Any<CancellationToken>());
    }

    private void Declare(params string[] ids)
    {
        _catalog.ListAsync(Arg.Any<CancellationToken>()).Returns([.. ids.Select(id => new CatalogModel(id, "loaded"))]);
    }
}
