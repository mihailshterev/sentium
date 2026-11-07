namespace Sentium.Locus.Contracts.Models;

/// <summary>
/// Search over the assets the household shares with assistants. All filters are optional and combine with AND.
/// </summary>
/// <param name="Query">Free text; every word must match an asset field, tag or containing location name.</param>
/// <param name="Location">A location id, path ("Home/Garage") or name; includes everything below it.</param>
public sealed record InventoryAssetQuery(
    string? Query,
    string? Location = null,
    string? Tag = null,
    string? Category = null,
    int Limit = 10);

public sealed record InventoryAssetSummary(
    Guid Id,
    string Name,
    string? Category,
    IReadOnlyList<string> Tags,
    string LocationPath);

public sealed record InventoryAttachmentInfo(string FileName, string Kind);

/// <summary>
/// Everything an assistant may know about a shared asset. Location descriptions and access notes are never
/// included.
/// </summary>
public sealed record InventoryAssetDetail(
    Guid Id,
    string Name,
    string? Category,
    IReadOnlyList<string> Tags,
    string LocationPath,
    string? Manufacturer,
    string? ModelNumber,
    string? SerialNumber,
    string? PhysicalDescription,
    string? Instructions,
    DateOnly? PurchaseDate,
    DateOnly? LastServicedDate,
    string? WarrantyInfo,
    string? AssistantNotes,
    IReadOnlyList<InventoryAttachmentInfo> Attachments);

public sealed record InventoryLocationInfo(Guid Id, string Name, string Path, int SubLocationCount);

public enum InventoryLocationLookupStatus
{
    Found,
    NotFound,

    /// <summary>The reference matched several locations; see <see cref="InventoryLocationLookup.Candidates"/>.</summary>
    Ambiguous
}

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
