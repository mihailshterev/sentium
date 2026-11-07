using Sentium.Locus.Contracts;
using Sentium.Locus.Contracts.Models;

namespace Sentium.Locus.Features.Agents;

/// <summary>
/// Singleton facade over <see cref="InventoryLookupService"/> for other modules: each call runs in its own
/// DI scope, so callers never depend on the Locus DbContext's lifetime.
/// </summary>
internal sealed class ScopedInventoryLookup(IServiceScopeFactory scopeFactory) : IInventoryLookup
{
    public Task<IReadOnlyList<InventoryAssetSummary>> FindAssetsAsync(InventoryAssetQuery query, CancellationToken ct = default)
        => RunAsync(s => s.FindAssetsAsync(query, ct));

    public Task<InventoryAssetDetail?> GetAssetAsync(Guid id, CancellationToken ct = default)
        => RunAsync(s => s.GetAssetAsync(id, ct));

    public Task<IReadOnlyList<InventoryAssetDetail>> FindAssetsByNameAsync(string name, int limit = 5, CancellationToken ct = default)
        => RunAsync(s => s.FindAssetsByNameAsync(name, limit, ct));

    public Task<InventoryLocationLookup> GetLocationContentsAsync(string? location, bool includeNested = false, int limit = 25, CancellationToken ct = default)
        => RunAsync(s => s.GetLocationContentsAsync(location, includeNested, limit, ct));

    private async Task<T> RunAsync<T>(Func<InventoryLookupService, Task<T>> action)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<InventoryLookupService>());
    }
}
