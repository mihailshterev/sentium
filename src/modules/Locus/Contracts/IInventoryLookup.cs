using Sentium.Locus.Contracts.Models;

namespace Sentium.Locus.Contracts;

/// <summary>
/// Read-only view of the household inventory for assistants. Only assets marked as shared with
/// assistants are ever returned; a private asset is indistinguishable from one that does not exist.
/// </summary>
public interface IInventoryLookup
{
    Task<IReadOnlyList<InventoryAssetSummary>> FindAssetsAsync(InventoryAssetQuery query, CancellationToken ct = default);

    Task<InventoryAssetDetail?> GetAssetAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Shared assets whose name equals <paramref name="name"/> (case-insensitive) or, failing that,
    /// contains it.
    /// </summary>
    Task<IReadOnlyList<InventoryAssetDetail>> FindAssetsByNameAsync(string name, int limit = 5, CancellationToken ct = default);

    /// <param name="location">A location id, path or name; <see langword="null"/> lists the top-level locations.</param>
    /// <param name="includeNested">Also list the assets of every location below it.</param>
    Task<InventoryLocationLookup> GetLocationContentsAsync(string? location, bool includeNested = false, int limit = 25, CancellationToken ct = default);
}
