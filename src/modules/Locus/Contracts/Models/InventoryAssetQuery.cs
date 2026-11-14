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
