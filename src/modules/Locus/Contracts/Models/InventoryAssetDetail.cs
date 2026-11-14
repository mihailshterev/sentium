namespace Sentium.Locus.Contracts.Models;

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
