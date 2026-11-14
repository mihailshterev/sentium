using System.ComponentModel;
using Microsoft.Extensions.AI;
using Sentium.Praxis.Contracts;
using Sentium.Tempus.Contracts;

namespace Sentium.Praxis.Features.Tools.Timeline;

/// <summary>
/// One task in full: description, deadline and the notes left on it.
/// </summary>
[AgentToolPolicy(RiskLevel = ToolRiskLevel.Low, Resource = ToolResource.Timeline, Action = ToolAction.Read)]
internal sealed class GetTimelineTaskTool(
    ITimelineAgentAccess timeline,
    IPdpContextAccessor pdpContext,
    TimeProvider timeProvider,
    ILogger<GetTimelineTaskTool> logger) : IAgentTool
{
    public AIFunction CreateFunction()
    {
        return AgentToolFunction.Create(
            this,
            GetAsync,
            ToolNames.GetTimelineTask,
            "Get one task from the user's timeline in full: description, planned day, due date, priority, status, tags, " +
            $"assigned agent and the notes left on it. Needs the task id from {ToolNames.ListTimelineTasks}.");
    }

    public async Task<string> GetAsync(
        [Description($"The task id (from {ToolNames.ListTimelineTasks}).")] string taskId,
        CancellationToken ct = default)
    {
        if (pdpContext.UserId is not { } userId || userId == Guid.Empty)
        {
            return TimelineToolMessages.NoUser;
        }

        if (!Guid.TryParse(TimelineToolText.Normalize(taskId), out var id))
        {
            return $"Error: taskId must be a task id from {ToolNames.ListTimelineTasks}.";
        }

        try
        {
            var detail = await timeline.GetTaskAsync(userId, id, ct);
            return detail is null
                ? $"No task with id {id} is on the user's timeline."
                : TimelineToolFormatter.FormatTask(detail, TimelineToolMessages.Today(timeProvider));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Timeline task lookup failed");
            return TimelineToolMessages.Unavailable;
        }
    }
}
