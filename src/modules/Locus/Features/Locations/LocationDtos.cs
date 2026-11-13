namespace Sentium.Locus.Features.Locations;

/// <summary>
/// A location with its position in the tree and aggregate counts. The API returns locations as a flat
/// list ordered by path; clients build the tree from <see cref="ParentLocationId"/>.
/// </summary>
public sealed record LocationDto(
    Guid Id,
    string Name,
    string? Description,
    string? AccessNotes,
    Guid? ParentLocationId,
    string Path,
    int Depth,
    int AssetCount,
    int SubtreeAssetCount,
    int ChildCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? CreatedBy,
    string? UpdatedBy);

/// <summary>
/// Create or update payload for a location. Changing <see cref="ParentLocationId"/> moves the location
/// together with everything below it.
/// </summary>
public sealed record SaveLocationRequest(
    string Name,
    string? Description,
    string? AccessNotes,
    Guid? ParentLocationId);
