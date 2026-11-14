namespace Sentium.Locus.Contracts.Models;

public sealed record InventoryAssetSummary(
    Guid Id,
    string Name,
    string? Category,
    IReadOnlyList<string> Tags,
    string LocationPath);
