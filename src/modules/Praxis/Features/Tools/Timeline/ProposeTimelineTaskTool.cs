using System.ComponentModel;
using Microsoft.Extensions.AI;
using Sentium.Praxis.Contracts;
using Sentium.Praxis.Features.Agents.Runtime;
using Sentium.Tempus.Contracts;
using Sentium.Tempus.Contracts.Models;

namespace Sentium.Praxis.Features.Tools.Timeline;

/// <summary>
/// Suggests a new task. It appears as a pending suggestion the user accepts or rejects - nothing is added until then.
/// </summary>
[AgentToolPolicy(RiskLevel = ToolRiskLevel.Low, Resource = ToolResource.Timeline, Action = ToolAction.Write)]
internal sealed class ProposeTimelineTaskTool(
    ITimelineAgentAccess timeline,
    IAgentRoster agentRoster,
    IPdpContextAccessor pdpContext,
    TimeProvider timeProvider,
    ILogger<ProposeTimelineTaskTool> logger) : IAgentTool
{
    public AIFunction CreateFunction()
    {
        return AgentToolFunction.Create(
            this,
            ProposeAsync,
            ToolNames.ProposeTimelineTask,
            "Suggest a new task for the user's timeline, on a day (date) or pinned to a month (month). The user sees it as a " +
            "suggestion and decides whether to add it - nothing changes until they accept. Check the workload with " +
            $"{ToolNames.GetTimelineOverview} first and prefer days with few open tasks.");
    }

    public async Task<string> ProposeAsync(
        [Description("Short task title, e.g. 'Book dentist appointment'.")] string title,
        [Description("The day for the task, YYYY-MM-DD. Leave empty to pin it to a month instead.")] string? date = null,
        [Description("The month to pin the task to when it has no day yet, YYYY-MM.")] string? month = null,
        [Description("low, normal, high or urgent (default normal).")] string? priority = null,
        [Description("Deadline, YYYY-MM-DD, if different from the day.")] string? dueDate = null,
        [Description("Comma-separated tags, e.g. 'health, family'.")] string? tags = null,
        [Description("Name of the agent to assign the task to (optional).")] string? assignedAgent = null,
        [Description("Optional details for the task.")] string? description = null,
        [Description("One sentence on why you suggest this, shown to the user.")] string? rationale = null,
        CancellationToken ct = default)
    {
        if (pdpContext.UserId is not { } userId || userId == Guid.Empty)
        {
            return TimelineToolMessages.NoUser;
        }

        var today = TimelineToolMessages.Today(timeProvider);

        if (TimelineToolText.Normalize(title) is not { } taskTitle)
        {
            return "Error: Give the task a title.";
        }

        if (TimelinePlacement.Parse(date, month, today, out var error) is not { } placement)
        {
            return error ?? $"Error: Give a date (YYYY-MM-DD) or a month (YYYY-MM). Today is {TimelineToolText.Format(today)}.";
        }

        var taskPriority = TimelineTaskPriority.Normal;
        if (TimelineToolText.Normalize(priority) is not null)
        {
            if (TimelineToolText.ParsePriority(priority) is not { } parsedPriority)
            {
                return "Error: priority must be one of: low, normal, high, urgent.";
            }

            taskPriority = parsedPriority;
        }

        DateOnly? due = null;
        if (TimelineToolText.Normalize(dueDate) is not null)
        {
            if (!TimelineToolText.TryParseDate(dueDate, today, out var parsedDue))
            {
                return TimelineToolText.DateError("dueDate", today);
            }

            due = parsedDue;
        }

        try
        {
            string? agent = null;
            if (TimelineToolText.Normalize(assignedAgent) is { } agentName)
            {
                var entry = await agentRoster.FindAsync(agentName, userId, ct);
                if (entry is null)
                {
                    var roster = await agentRoster.GetAsync(userId, ct);
                    return $"Error: There is no agent named '{agentName}'. Leave assignedAgent empty or use one of: {string.Join(", ", roster.Select(r => r.Name))}.";
                }

                agent = entry.Name;
            }

            var proposal = new TimelineTaskProposal(
                taskTitle,
                TimelineToolText.Normalize(description),
                placement.Year,
                placement.Month,
                placement.Date,
                due,
                taskPriority,
                TimelineToolText.SplitTags(tags),
                agent,
                TimelineToolText.Normalize(rationale));

            var result = await timeline.ProposeTaskAsync(userId, AgentToolFunction.CurrentAgentName, proposal, ct);
            if (!result.IsSuccess)
            {
                return TimelineWriteResults.Describe(result);
            }

            return result.Value!.AlreadyPending
                ? $"The same suggestion ('{TimelineToolFormatter.Title(taskTitle)}' on {placement.Describe()}) is already waiting for the user's review."
                : $"Suggested '{TimelineToolFormatter.Title(taskTitle)}' for {placement.Describe()}. It is a proposal awaiting the user's review - the task is not on the timeline until they accept it.";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Timeline task proposal failed");
            return TimelineToolMessages.WriteFailed;
        }
    }
}
