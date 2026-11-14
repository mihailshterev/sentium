namespace Sentium.Tempus.Contracts.Models;

/// <summary>
/// Open Tasks and Calendar Events planned on one day.
/// </summary>
public sealed record TimelineDayLoad(DateOnly Date, int Open, int Events = 0);
