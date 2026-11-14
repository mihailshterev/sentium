namespace Sentium.Tempus.Contracts.Models;

/// <summary>
/// Entries planned between <paramref name="From"/> and <paramref name="To"/> (inclusive). Entries pinned to a month
/// (no day yet) are included when that month overlaps the range and <paramref name="IncludePinned"/> is set.
/// </summary>
/// <param name="Kind">Only Tasks or only Calendar Events; <see langword="null"/> for both.</param>
public sealed record TimelineTaskQuery(
    DateOnly From,
    DateOnly To,
    TimelineTaskStatus? Status = null,
    string? Tag = null,
    string? AssignedAgent = null,
    bool IncludePinned = true,
    int Limit = 50,
    TimelineEntryKind? Kind = null);
