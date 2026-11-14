namespace Sentium.Tempus.Contracts.Models;

/// <summary>
/// Open tasks planned on one day.
/// </summary>
public sealed record TimelineDayLoad(DateOnly Date, int Open);
