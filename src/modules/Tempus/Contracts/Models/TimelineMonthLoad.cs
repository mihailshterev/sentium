namespace Sentium.Tempus.Contracts.Models;

/// <summary>
/// Workload of one month of the overview year.
/// </summary>
public sealed record TimelineMonthLoad(int Month, int Open, int Done, int Overdue, int PendingProposals);
