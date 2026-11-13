using Sentium.Tempus.Contracts.Models;

namespace Sentium.Tempus.Features.Tasks;

public sealed record TimelineTaskDto(
    Guid Id,
    string Title,
    string? Description,
    int Year,
    int Month,
    DateOnly? Date,
    DateOnly? DueDate,
    TimelineTaskPriority Priority,
    TimelineTaskStatus Status,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<string> Tags,
    string? AssignedAgent,
    TimelineAuthorKind Origin,
    string? CreatedByAgent,
    int NoteCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
/// Creates or replaces a task. <paramref name="Date"/> <see langword="null"/> pins it to
/// <paramref name="Year"/>/<paramref name="Month"/>.
/// </summary>
public sealed record SaveTimelineTaskRequest(
    string Title,
    string? Description,
    int Year,
    int Month,
    DateOnly? Date,
    DateOnly? DueDate,
    TimelineTaskPriority Priority = TimelineTaskPriority.Normal,
    TimelineTaskStatus Status = TimelineTaskStatus.Todo,
    IReadOnlyList<string>? Tags = null,
    string? AssignedAgent = null);

/// <summary>
/// Moves a task to a day (<paramref name="Date"/>) or pins it to a month.
/// </summary>
public sealed record ScheduleTimelineTaskRequest(int Year, int Month, DateOnly? Date);

public sealed record SetTimelineTaskStatusRequest(TimelineTaskStatus Status);
