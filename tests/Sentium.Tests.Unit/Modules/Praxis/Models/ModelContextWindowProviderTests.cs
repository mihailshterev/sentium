using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sentium.Praxis.Features.Models;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis.Models;

public sealed class ModelContextWindowProviderTests : IDisposable
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly IModelCatalog _catalog = Substitute.For<IModelCatalog>();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    private CatalogModelContextWindowProvider CreateProvider() => new(_catalog, _cache, NullLogger<CatalogModelContextWindowProvider>.Instance);

    public void Dispose() => _cache.Dispose();

    [Fact]
    public async Task UsesThePerSlotWindowTheServerReports_AndCachesEveryReportedModel()
    {
        _catalog.ListAsync(Arg.Any<CancellationToken>()).Returns([
            new CatalogModel("gemma-4-e4b", "loaded", 16384),
            new CatalogModel("qwen-coder", "loaded", 8192),
        ]);
        var provider = CreateProvider();

        Assert.Equal(16384, await provider.GetContextWindowAsync("gemma-4-e4b", Ct));
        Assert.Equal(8192, await provider.GetContextWindowAsync("qwen-coder", Ct));

        await _catalog.Received(1).ListAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnloadedModel_FallsBackToTheDefaultWindow()
    {
        _catalog.ListAsync(Arg.Any<CancellationToken>()).Returns([new CatalogModel("gemma-4-e4b", "unloaded")]);

        Assert.Equal(CatalogModelContextWindowProvider.DefaultContextWindow, await CreateProvider().GetContextWindowAsync("gemma-4-e4b", Ct));
    }

    [Fact]
    public async Task UnreachableServer_FallsBackToTheDefaultWindow_WithoutRetryingOnEveryAgent()
    {
        _catalog.ListAsync(Arg.Any<CancellationToken>()).Returns((IReadOnlyList<CatalogModel>?)null);
        var provider = CreateProvider();

        Assert.Equal(CatalogModelContextWindowProvider.DefaultContextWindow, await provider.GetContextWindowAsync("gemma-4-e4b", Ct));
        Assert.Equal(CatalogModelContextWindowProvider.DefaultContextWindow, await provider.GetContextWindowAsync("gemma-4-e4b", Ct));

        await _catalog.Received(1).ListAsync(Arg.Any<CancellationToken>());
    }
}
