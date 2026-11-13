using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Sentium.Core.Results;
using Sentium.Locus.Entities;
using Sentium.Locus.Features.Attachments;
using Sentium.Locus.Features.Locations;

namespace Sentium.Locus.Features.Assets;

/// <summary>
/// Where an asset's knowledge-base copy stands.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<AssetKnowledgeStatus>))]
public enum AssetKnowledgeStatus
{
    /// <summary>
    /// The asset is private; assistants cannot see it.
    /// </summary>
    NotShared,

    /// <summary>
    /// A knowledge-base update (or removal) is queued.
    /// </summary>
    Pending,

    /// <summary>
    /// The knowledge base holds the current content.
    /// </summary>
    Synced,

    /// <summary>
    /// Syncing failed repeatedly; edit the asset or retry manually.
    /// </summary>
    Failed
}

[JsonConverter(typeof(JsonStringEnumConverter<AssetSort>))]
public enum AssetSort
{
    Name,
    Updated,
    Created
}

/// <summary>
/// One row of the asset list.
/// </summary>
public sealed record AssetSummaryDto(
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

/// <summary>
/// Full asset detail.
/// </summary>
public sealed record AssetDto(
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

/// <summary>
/// Create or update payload for an asset.
/// </summary>
public sealed record SaveAssetRequest(
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

/// <summary>
/// Query string of <c>GET api/locus/assets</c>. Every filter is optional and they combine with AND;
/// <see cref="Tags"/> (repeat <c>tag=</c>) requires every listed tag.
/// </summary>
public sealed record AssetSearchQuery : PaginationQuery
{
    /// <summary>
    /// Free text; every word must match a field, a tag or the name of a containing location.
    /// </summary>
    public string? Q { get; init; }

    public Guid? LocationId { get; init; }

    /// <summary>
    /// With <see cref="LocationId"/>: also include assets in sub-locations (default).
    /// </summary>
    public bool IncludeSubLocations { get; init; } = true;

    public string? Category { get; init; }

    [FromQuery(Name = "tag")]
    public IReadOnlyList<string>? Tags { get; init; }

    public bool? AgentAccessible { get; init; }

    public AssetSort Sort { get; init; } = AssetSort.Name;
}

public sealed record TagCountDto(string Name, int Count);
