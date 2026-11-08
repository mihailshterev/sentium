namespace Sentium.Tempus.Contracts.Models;

/// <summary>
/// Workload of one month of the overview year.
/// </summary>
public sealed record TimelineMonthLoad(int Month, int Open, int Done, int Overdue, int PendingProposals);

/// <summary>
/// Open tasks planned on one day.
/// </summary>
public sealed record TimelineDayLoad(DateOnly Date, int Open);

/// <summary>
/// What an assistant needs to judge the user's workload: the year by month, the coming days by day, and how many
/// suggestions are still waiting for the user.
/// </summary>
/// <param name="Today">The current UTC date.</param>
public sealed record TimelineAgentOverview(
    DateOnly Today,
    int Year,
    IReadOnlyList<TimelineMonthLoad> Months,
    IReadOnlyList<TimelineDayLoad> UpcomingDays,
    int PinnedOpenThisMonth,
    int PendingProposals);

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

public sealed record TimelineNoteInfo(
    Guid Id,
    TimelineNoteKind Kind,
    TimelineAuthorKind Author,
    string? AuthorName,
    string Content,
    DateTimeOffset CreatedAt);

public sealed record TimelineTaskDetail(
    TimelineTaskInfo Task,
    string? Description,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<TimelineNoteInfo> Notes,
    int PendingProposals);

/// <summary>
/// A task an assistant suggests adding. <paramref name="Date"/> <see langword="null"/> pins it to the month.
/// </summary>
public sealed record TimelineTaskProposal(
    string Title,
    string? Description,
    int Year,
    int Month,
    DateOnly? Date,
    DateOnly? DueDate,
    TimelineTaskPriority Priority,
    IReadOnlyList<string> Tags,
    string? AssignedAgent,
    string? Rationale);

/// <summary>
/// A new place on the timeline an assistant suggests for an existing task. <paramref name="Date"/>
/// <see langword="null"/> pins it to the month.
/// </summary>
public sealed record TimelineRescheduleProposal(Guid TaskId, int Year, int Month, DateOnly? Date, string? Rationale);

/// <summary>
/// A note an assistant leaves. It is attached to the task when <paramref name="TaskId"/> is set, else to the day
/// when <paramref name="Date"/> is set, else to <paramref name="Year"/>/<paramref name="Month"/>, else to the
/// current month.
/// </summary>
public sealed record TimelineNoteInput(
    TimelineNoteKind Kind,
    string Content,
    Guid? TaskId = null,
    int? Year = null,
    int? Month = null,
    DateOnly? Date = null);

/// <param name="AlreadyPending">An identical proposal was already waiting, so no new one was created.</param>
/// <param name="Superseded">Older pending proposals this one replaced.</param>
public sealed record TimelineProposalReceipt(Guid ProposalId, bool AlreadyPending, int Superseded);

/// <summary>
/// Where a note ended up.
/// </summary>
public sealed record TimelineNoteReceipt(Guid NoteId, int Year, int Month, DateOnly? Date, Guid? TaskId);
