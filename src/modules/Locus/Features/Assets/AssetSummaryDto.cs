namespace Sentium.Locus.Features.Assets;

/// <summary>
/// One row of the asset list.
/// </summary>
internal sealed record AssetSummaryDto(
    Guid Id,
    string DisplayName,
    string? Category,
    IReadOnlyList<string> Tags,
    string? Manufacturer,
    string? ModelNumber,
    Guid LocationId,
    string LocationPath,
    bool IsAgentAccessible,
    AssetKnowledgeStatus KnowledgeStatus,
    int AttachmentCount,
    string? CoverImageUrl,
    DateTimeOffset UpdatedAt);
