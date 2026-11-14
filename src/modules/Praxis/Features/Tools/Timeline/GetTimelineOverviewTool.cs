using System.ComponentModel;
using System.Globalization;
using Microsoft.Extensions.AI;
using Sentium.Praxis.Contracts;
using Sentium.Tempus.Contracts;

namespace Sentium.Praxis.Features.Tools.Timeline;

/// <summary>
/// The user's workload at a glance: the year by month and the coming days by day.
/// </summary>
[AgentToolPolicy(RiskLevel = ToolRiskLevel.Low, Resource = ToolResource.Timeline, Action = ToolAction.Read)]
internal sealed class GetTimelineOverviewTool(
    ITimelineAgentAccess timeline,
    IPdpContextAccessor pdpContext,
    TimeProvider timeProvider,
    ILogger<GetTimelineOverviewTool> logger) : IAgentTool
{
    private const int DefaultDays = 14;

    public AIFunction CreateFunction()
    {
        return AgentToolFunction.Create(
            this,
            GetAsync,
            ToolNames.GetTimelineOverview,
            "Summarise the user's year timeline (their personal planner): open, done and overdue tasks per month, open tasks " +
            "per day for the coming days, and suggestions waiting for their review. Call this first to judge the workload " +
            $"before suggesting or moving tasks; use {ToolNames.ListTimelineTasks} for the tasks themselves.");
    }

    public async Task<string> GetAsync(
        [Description("The year to summarise, e.g. 2026 (default: this year).")] string? year = null,
        [Description("How many days from today to break down by day (1-42, default 14).")] int? days = null,
        CancellationToken ct = default)
    {
        if (pdpContext.UserId is not { } userId || userId == Guid.Empty)
        {
            return TimelineToolMessages.NoUser;
        }

        int? requestedYear = null;
        if (TimelineToolText.Normalize(year) is { } yearText)
        {
            if (!int.TryParse(yearText, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
            {
                return $"Error: year must be a number like {TimelineToolMessages.Today(timeProvider).Year}.";
            }

            requestedYear = parsed;
        }

        try
        {
            var overview = await timeline.GetOverviewAsync(userId, requestedYear, days ?? DefaultDays, ct);
            return TimelineToolFormatter.FormatOverview(overview);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Timeline overview failed");
            return TimelineToolMessages.Unavailable;
        }
    }
}
