using Sentium.Tempus.Contracts.Models;

namespace Sentium.Tempus.Dtos;

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

public sealed record TimelineNoteDto(
    Guid Id,
    int Year,
    int Month,
    DateOnly? Date,
    Guid? TaskId,
    TimelineNoteKind Kind,
    TimelineAuthorKind Author,
    string? AuthorName,
    string Content,
    DateTimeOffset CreatedAt);

public sealed record TimelineProposalDto(
    Guid Id,
    TimelineProposalKind Kind,
    TimelineProposalStatus Status,
    Guid? TaskId,
    string Title,
    string? Description,
    TimelineTaskPriority Priority,
    IReadOnlyList<string> Tags,
    string? AssignedAgent,
    DateOnly? DueDate,
    int Year,
    int Month,
    DateOnly? Date,
    int? FromYear,
    int? FromMonth,
    DateOnly? FromDate,
    string? Rationale,
    string ProposedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt);

/// <summary>
/// One month of the year overview: the badges of its node.
/// </summary>
/// <param name="OverdueCount">Open tasks whose deadline (due date, else planned day, else month end) has passed.</param>
/// <param name="LastAgentActivityAt">The latest agent note or pending proposal in the month.</param>
public sealed record MonthSummaryDto(
    int Month,
    int TaskCount,
    int OpenCount,
    int DoneCount,
    int OverdueCount,
    int AgentAssignedCount,
    int NoteCount,
    int AgentNoteCount,
    int PendingProposalCount,
    DateTimeOffset? LastAgentActivityAt);

/// <param name="Today">The date overdue counts were computed for.</param>
/// <param name="Months">Always twelve entries, January first.</param>
public sealed record YearOverviewDto(int Year, DateOnly Today, IReadOnlyList<MonthSummaryDto> Months);

/// <param name="Proposals">Pending proposals that target this month or move a task out of it.</param>
public sealed record MonthDetailDto(
    int Year,
    int Month,
    IReadOnlyList<TimelineTaskDto> Tasks,
    IReadOnlyList<TimelineNoteDto> Notes,
    IReadOnlyList<TimelineProposalDto> Proposals);

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

/// <summary>
/// A note by the user on a task (<paramref name="TaskId"/>), a day (<paramref name="Date"/>) or a month. A task
/// note is placed where the task is.
/// </summary>
public sealed record CreateTimelineNoteRequest(int Year, int Month, DateOnly? Date, Guid? TaskId, string Content);
