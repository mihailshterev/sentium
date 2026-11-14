namespace Sentium.Tempus.Contracts.Models;

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
