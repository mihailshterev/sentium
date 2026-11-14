namespace Sentium.Tempus.Contracts.Models;

/// <summary>
/// Tasks planned between <paramref name="From"/> and <paramref name="To"/> (inclusive). Tasks pinned to a month
/// (no day yet) are included when that month overlaps the range and <paramref name="IncludePinned"/> is set.
/// </summary>
public sealed record TimelineTaskQuery(
    DateOnly From,
    DateOnly To,
    TimelineTaskStatus? Status = null,
    string? Tag = null,
    string? AssignedAgent = null,
    bool IncludePinned = true,
    int Limit = 50);
