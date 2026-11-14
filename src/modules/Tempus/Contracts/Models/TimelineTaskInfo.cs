namespace Sentium.Tempus.Contracts.Models;

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
    string? AssignedAgent);
