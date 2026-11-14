namespace Sentium.Locus.Features.Locations;

internal enum LocationDeleteStatus
{
    Deleted,
    NotFound,

    /// <summary>
    /// The location still has sub-locations or assets and no move target was given.
    /// </summary>
    NotEmpty,

    /// <summary>
    /// The move target does not exist or is the location itself.
    /// </summary>
    InvalidTarget,

    /// <summary>
    /// The move would break a rule (cycle, depth, duplicate sibling name).
    /// </summary>
    Conflict
}
