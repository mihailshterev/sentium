namespace Sentium.Locus.Core.Dtos;

/// <summary>Represents a location in the home hierarchy returned to callers.</summary>
public sealed record LocationDto(
    Guid Id,
    string Name,
    string? Description,
    string? AccessNotes,
    Guid? ParentLocationId,
    int AssetCount,
    int SubLocationCount,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>Represents an asset in the digital twin inventory returned to callers.</summary>
public sealed record AssetDto(
    Guid Id,
    string DisplayName,
    string? Category,
    string? PhysicalDescription,
    string? Instructions,
    string? Manufacturer,
    string? ModelNumber,
    string? SerialNumber,
    DateTime? PurchaseDate,
    DateTime? LastServicedDate,
    string? WarrantyInfo,
    bool IsAgentAccessible,
    string? AgentInstructions,
    Guid LocationId,
    string LocationName,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>Request to create a new location.</summary>
public sealed record CreateLocationRequest(
    string Name,
    string? Description,
    string? AccessNotes,
    Guid? ParentLocationId);

/// <summary>Request to update an existing location.</summary>
public sealed record UpdateLocationRequest(
    string Name,
    string? Description,
    string? AccessNotes,
    Guid? ParentLocationId);

/// <summary>Request to create a new asset.</summary>
public sealed record CreateAssetRequest(
    string DisplayName,
    string? Category,
    string? PhysicalDescription,
    string? Instructions,
    string? Manufacturer,
    string? ModelNumber,
    string? SerialNumber,
    DateTime? PurchaseDate,
    DateTime? LastServicedDate,
    string? WarrantyInfo,
    bool IsAgentAccessible,
    string? AgentInstructions,
    Guid LocationId);

/// <summary>Request to update an existing asset.</summary>
public sealed record UpdateAssetRequest(
    string DisplayName,
    string? Category,
    string? PhysicalDescription,
    string? Instructions,
    string? Manufacturer,
    string? ModelNumber,
    string? SerialNumber,
    DateTime? PurchaseDate,
    DateTime? LastServicedDate,
    string? WarrantyInfo,
    bool IsAgentAccessible,
    string? AgentInstructions,
    Guid LocationId);
