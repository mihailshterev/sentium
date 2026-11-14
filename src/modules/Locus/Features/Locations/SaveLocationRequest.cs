namespace Sentium.Locus.Features.Locations;

/// <summary>
/// Create or update payload for a location. Changing <see cref="ParentLocationId"/> moves the location
/// together with everything below it.
/// </summary>
internal sealed record SaveLocationRequest(
    string Name,
    string? Description,
    string? AccessNotes,
    Guid? ParentLocationId);
