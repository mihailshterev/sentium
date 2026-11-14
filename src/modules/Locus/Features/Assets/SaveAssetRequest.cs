namespace Sentium.Locus.Features.Assets;

/// <summary>
/// Create or update payload for an asset.
/// </summary>
internal sealed record SaveAssetRequest(
    string DisplayName,
    Guid LocationId,
    string? Category = null,
    string? PhysicalDescription = null,
    string? Instructions = null,
    string? Manufacturer = null,
    string? ModelNumber = null,
    string? SerialNumber = null,
    DateOnly? PurchaseDate = null,
    DateOnly? LastServicedDate = null,
    string? WarrantyInfo = null,
    bool IsAgentAccessible = true,
    string? AgentInstructions = null,
    IReadOnlyList<string>? Tags = null);
