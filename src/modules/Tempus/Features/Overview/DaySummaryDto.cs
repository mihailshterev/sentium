namespace Sentium.Tempus.Features.Overview;

/// <summary>
/// The tasks planned on one day of the year overview: the ticks along the year path.
/// </summary>
/// <param name="Urgent">An open task that day is high/urgent priority or overdue.</param>
public sealed record DaySummaryDto(DateOnly Date, int Open, int Done, bool Urgent);
