namespace Sentium.Tempus.Features.Overview;

/// <summary>
/// One month of the year overview: the badges of its node.
/// </summary>
/// <param name="OverdueCount">Open tasks whose deadline (due date, else planned day, else month end) has passed.</param>
/// <param name="LastAgentActivityAt">The latest agent note or pending proposal in the month.</param>
internal sealed record MonthSummaryDto(
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
