using Sentium.Tempus.Contracts.Models;
using Sentium.Tempus.Features.Notes;
using Sentium.Tempus.Features.Proposals;
using Sentium.Tempus.Features.Tasks;

namespace Sentium.Tempus.Features.Overview;

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

/// <summary>
/// The tasks planned on one day of the year overview: the ticks along the year path.
/// </summary>
/// <param name="Urgent">An open task that day is high/urgent priority or overdue.</param>
public sealed record DaySummaryDto(DateOnly Date, int Open, int Done, bool Urgent);

/// <param name="Today">The date overdue counts were computed for.</param>
/// <param name="Months">Always twelve entries, January first.</param>
/// <param name="Days">Only days with tasks planned on them, in date order.</param>
public sealed record YearOverviewDto(
    int Year,
    DateOnly Today,
    IReadOnlyList<MonthSummaryDto> Months,
    IReadOnlyList<DaySummaryDto> Days);

/// <summary>
/// What needs attention now, across years: overdue tasks and the open tasks of the coming days.
/// </summary>
/// <param name="Today">The date the lists were computed for.</param>
/// <param name="Days">Length of the <paramref name="Next"/> window, today included.</param>
/// <param name="Overdue">Open tasks whose deadline has passed, oldest deadline first (the first few).</param>
/// <param name="OverdueCount">All overdue tasks, also those not listed.</param>
/// <param name="Next">Open tasks planned from today to the last day of the window, by day (the first few).</param>
/// <param name="PendingProposals">Agent suggestions still waiting for an answer.</param>
public sealed record UpcomingDto(
    DateOnly Today,
    int Days,
    IReadOnlyList<TimelineTaskDto> Overdue,
    int OverdueCount,
    IReadOnlyList<TimelineTaskDto> Next,
    int PendingProposals);

/// <param name="Proposals">Pending proposals that target this month or move a task out of it.</param>
public sealed record MonthDetailDto(
    int Year,
    int Month,
    IReadOnlyList<TimelineTaskDto> Tasks,
    IReadOnlyList<TimelineNoteDto> Notes,
    IReadOnlyList<TimelineProposalDto> Proposals);
