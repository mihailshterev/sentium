using Microsoft.AspNetCore.Mvc;
using Sentium.Core.Results;

namespace Sentium.Locus.Features.Assets;

/// <summary>
/// Query string of <c>GET api/locus/assets</c>. Every filter is optional and they combine with AND;
/// <see cref="Tags"/> (repeat <c>tag=</c>) requires every listed tag.
/// </summary>
internal sealed record AssetSearchQuery : PaginationQuery
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
