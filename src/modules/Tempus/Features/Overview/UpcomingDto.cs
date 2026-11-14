using Sentium.Tempus.Features.Tasks;

namespace Sentium.Tempus.Features.Overview;

/// <summary>
/// What needs attention now, across years: overdue tasks and the open tasks of the coming days.
/// </summary>
/// <param name="Today">The date the lists were computed for.</param>
/// <param name="Days">Length of the <paramref name="Next"/> window, today included.</param>
/// <param name="Overdue">Open tasks whose deadline has passed, oldest deadline first (the first few).</param>
/// <param name="OverdueCount">All overdue tasks, also those not listed.</param>
/// <param name="Next">Open tasks planned from today to the last day of the window, by day (the first few).</param>
/// <param name="PendingProposals">Agent suggestions still waiting for an answer.</param>
internal sealed record UpcomingDto(
    DateOnly Today,
    int Days,
    IReadOnlyList<TimelineTaskDto> Overdue,
    int OverdueCount,
    IReadOnlyList<TimelineTaskDto> Next,
    int PendingProposals);
