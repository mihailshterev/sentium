namespace Sentium.Tempus.Features.Overview;

/// <summary>
/// One day of the year overview with Entries on it: the ticks along the year path. <paramref name="Open"/>,
/// <paramref name="Done"/> and <paramref name="Urgent"/> count Tasks only.
/// </summary>
/// <param name="Urgent">An open task that day is high/urgent priority or overdue.</param>
/// <param name="Events">Calendar Events on the day, including every day of a multi-day one.</param>
internal sealed record DaySummaryDto(DateOnly Date, int Open, int Done, bool Urgent, int Events);
