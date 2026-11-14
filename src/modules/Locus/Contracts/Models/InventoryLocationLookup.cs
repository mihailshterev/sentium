namespace Sentium.Locus.Contracts.Models;

/// <summary>
/// What a location holds. With no location given, <see cref="SubLocations"/> lists the top-level locations.
/// </summary>
public sealed record InventoryLocationLookup(
    InventoryLocationLookupStatus Status,
    InventoryLocationInfo? Location,
    IReadOnlyList<InventoryLocationInfo> SubLocations,
    IReadOnlyList<InventoryAssetSummary> Assets,
    IReadOnlyList<InventoryLocationInfo> Candidates,
    bool Truncated);
