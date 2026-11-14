namespace Sentium.Tempus.Contracts.Models;

/// <summary>
/// Workload of one month of the overview year. <paramref name="Open"/>, <paramref name="Done"/> and
/// <paramref name="Overdue"/> count Tasks only.
/// </summary>
/// <param name="Events">Calendar Events in the month.</param>
public sealed record TimelineMonthLoad(int Month, int Open, int Done, int Overdue, int PendingProposals, int Events = 0);
