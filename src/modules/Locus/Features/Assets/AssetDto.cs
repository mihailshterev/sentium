using Sentium.Locus.Features.Attachments;
using Sentium.Locus.Features.Locations;

namespace Sentium.Locus.Features.Assets;

/// <summary>
/// Full asset detail.
/// </summary>
internal sealed record AssetDto(
    Guid Id,
    string DisplayName,
    string? Category,
    string? PhysicalDescription,
    string? Instructions,
    string? Manufacturer,
    string? ModelNumber,
    string? SerialNumber,
    DateOnly? PurchaseDate,
    DateOnly? LastServicedDate,
    string? WarrantyInfo,
    bool IsAgentAccessible,
    string? AgentInstructions,
    IReadOnlyList<string> Tags,
    Guid LocationId,
    string LocationPath,
    IReadOnlyList<LocationCrumb> LocationCrumbs,
    AssetKnowledgeStatus KnowledgeStatus,
    string? KnowledgeError,
    DateTimeOffset? KnowledgeSyncedAt,
    IReadOnlyList<AttachmentDto> Attachments,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? CreatedBy,
    string? UpdatedBy);
