namespace Sentium.Tempus.Contracts.Models;

/// <param name="IsPast">A Calendar Event that is over; never set for a Task.</param>
/// <param name="Repeats">The Entry is one occurrence of a Series.</param>
public sealed record TimelineTaskInfo(
    Guid Id,
    string Title,
    int Year,
    int Month,
    DateOnly? Date,
    DateOnly? DueDate,
    TimelineTaskPriority Priority,
    TimelineTaskStatus Status,
    IReadOnlyList<string> Tags,
    string? AssignedAgent,
    TimelineEntryKind Kind = TimelineEntryKind.Task,
    TimeOnly? StartTime = null,
    DateOnly? EndDate = null,
    bool IsPast = false,
    bool Repeats = false);
