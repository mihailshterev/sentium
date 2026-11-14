using System.ComponentModel;
using Microsoft.Extensions.AI;
using Sentium.Praxis.Contracts;
using Sentium.Tempus.Contracts;
using Sentium.Tempus.Contracts.Models;

namespace Sentium.Praxis.Features.Tools.Timeline;

/// <summary>
/// Suggests a new day or month for an existing task; the user accepts or rejects the move.
/// </summary>
[AgentToolPolicy(RiskLevel = ToolRiskLevel.Low, Resource = ToolResource.Timeline, Action = ToolAction.Write)]
internal sealed class ProposeTaskRescheduleTool(
    ITimelineAgentAccess timeline,
    IPdpContextAccessor pdpContext,
    TimeProvider timeProvider,
    ILogger<ProposeTaskRescheduleTool> logger) : IAgentTool
{
    public AIFunction CreateFunction()
    {
        return AgentToolFunction.Create(
            this,
            ProposeAsync,
            ToolNames.ProposeTaskReschedule,
            "Suggest moving a task on the user's timeline to another day (date) or to a month without a day (month), e.g. to " +
            "spread out an overloaded week. The user accepts or rejects the move - nothing changes until then. A newer " +
            "suggestion for the same task replaces the older one.");
    }

    public async Task<string> ProposeAsync(
        [Description($"The task id (from {ToolNames.ListTimelineTasks}).")] string taskId,
        [Description("The new day, YYYY-MM-DD. Leave empty to pin the task to a month instead.")] string? date = null,
        [Description("The month to pin the task to without a day, YYYY-MM.")] string? month = null,
        [Description("One sentence on why the move helps, shown to the user.")] string? rationale = null,
        CancellationToken ct = default)
    {
        if (pdpContext.UserId is not { } userId || userId == Guid.Empty)
        {
            return TimelineToolMessages.NoUser;
        }

        var today = TimelineToolMessages.Today(timeProvider);

        if (!Guid.TryParse(TimelineToolText.Normalize(taskId), out var id))
        {
            return $"Error: taskId must be a task id from {ToolNames.ListTimelineTasks}.";
        }

        if (TimelinePlacement.Parse(date, month, today, out var error) is not { } placement)
        {
            return error ?? $"Error: Give the new date (YYYY-MM-DD) or month (YYYY-MM). Today is {TimelineToolText.Format(today)}.";
        }

        try
        {
            var result = await timeline.ProposeRescheduleAsync(
                userId,
                AgentToolFunction.CurrentAgentName,
                new TimelineRescheduleProposal(id, placement.Year, placement.Month, placement.Date, TimelineToolText.Normalize(rationale)),
                ct);

            if (!result.IsSuccess)
            {
                return TimelineWriteResults.Describe(result);
            }

            var replaced = result.Value!.Superseded > 0 ? " It replaces your earlier suggestion for this task." : string.Empty;
            return $"Suggested moving the task to {placement.Describe()}. It is a proposal awaiting the user's review - the task stays where it is until they accept.{replaced}";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Timeline reschedule proposal failed");
            return TimelineToolMessages.WriteFailed;
        }
    }
}
