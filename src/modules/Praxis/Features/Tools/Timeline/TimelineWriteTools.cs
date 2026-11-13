using System.ComponentModel;
using Microsoft.Extensions.AI;
using Sentium.Core.Results;
using Sentium.Praxis.Contracts;
using Sentium.Praxis.Features.Agents;
using Sentium.Praxis.Features.Sentinel;
using Sentium.Tempus.Contracts;
using Sentium.Tempus.Contracts.Models;

namespace Sentium.Praxis.Features.Tools.Timeline;

/// <summary>
/// Where on the timeline something goes: a day, or a month without a day.
/// </summary>
internal readonly record struct TimelinePlacement(int Year, int Month, DateOnly? Date)
{
    public string Describe() => Date is { } date
        ? $"{TimelineToolText.Format(date)} ({TimelineToolText.Weekday(date)})"
        : $"{TimelineToolText.FormatMonth(Year, Month)} (pinned to the month)";

    /// <summary>
    /// A day when <paramref name="date"/> is given, else the month; <see langword="null"/> with an error otherwise.
    /// </summary>
    public static TimelinePlacement? Parse(string? date, string? month, DateOnly today, out string? error)
    {
        error = null;

        if (TimelineToolText.Normalize(date) is not null)
        {
            if (!TimelineToolText.TryParseDate(date, today, out var day))
            {
                error = TimelineToolText.DateError("date", today);
                return null;
            }

            return new TimelinePlacement(day.Year, day.Month, day);
        }

        if (TimelineToolText.Normalize(month) is not null)
        {
            if (!TimelineToolText.TryParseMonth(month, today, out var year, out var monthNumber))
            {
                error = TimelineToolText.MonthError("month", today);
                return null;
            }

            return new TimelinePlacement(year, monthNumber, null);
        }

        return null;
    }
}

internal static class TimelineWriteResults
{
    public static string Describe<T>(Result<T> result) => result.Status switch
    {
        ResultStatus.NotFound => $"Error: {result.Error ?? "Not found."} Use {ToolNames.ListTimelineTasks} to find task ids.",
        ResultStatus.Conflict => $"Error: {result.Error}",
        _ => $"Error: {result.Error ?? "The request was invalid."}"
    };
}

/// <summary>
/// Suggests a new task. It appears as a pending suggestion the user accepts or rejects - nothing is added until then.
/// </summary>
[AgentToolPolicy(RiskLevel = ToolRiskLevel.Low, Resource = ToolResource.Timeline, Action = ToolAction.Write)]
public sealed class ProposeTimelineTaskTool(
    ITimelineAgentAccess timeline,
    IAgentRoster agentRoster,
    IPdpContextAccessor pdpContext,
    TimeProvider timeProvider,
    ILogger<ProposeTimelineTaskTool> logger) : IAgentTool
{
    public AIFunction CreateFunction() => AgentToolFunction.Create(
        this,
        ProposeAsync,
        ToolNames.ProposeTimelineTask,
        "Suggest a new task for the user's timeline, on a day (date) or pinned to a month (month). The user sees it as a " +
        "suggestion and decides whether to add it - nothing changes until they accept. Check the workload with " +
        $"{ToolNames.GetTimelineOverview} first and prefer days with few open tasks.");

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

/// <summary>
/// Suggests a new day or month for an existing task; the user accepts or rejects the move.
/// </summary>
[AgentToolPolicy(RiskLevel = ToolRiskLevel.Low, Resource = ToolResource.Timeline, Action = ToolAction.Write)]
public sealed class ProposeTaskRescheduleTool(
    ITimelineAgentAccess timeline,
    IPdpContextAccessor pdpContext,
    TimeProvider timeProvider,
    ILogger<ProposeTaskRescheduleTool> logger) : IAgentTool
{
    public AIFunction CreateFunction() => AgentToolFunction.Create(
        this,
        ProposeAsync,
        ToolNames.ProposeTaskReschedule,
        "Suggest moving a task on the user's timeline to another day (date) or to a month without a day (month), e.g. to " +
        "spread out an overloaded week. The user accepts or rejects the move - nothing changes until then. A newer " +
        "suggestion for the same task replaces the older one.");

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

/// <summary>
/// Leaves a note, status update or progress report on a task, a day or a month of the user's timeline.
/// </summary>
[AgentToolPolicy(RiskLevel = ToolRiskLevel.Low, Resource = ToolResource.Timeline, Action = ToolAction.Write)]
public sealed class AddTimelineNoteTool(
    ITimelineAgentAccess timeline,
    IPdpContextAccessor pdpContext,
    TimeProvider timeProvider,
    ILogger<AddTimelineNoteTool> logger) : IAgentTool
{
    public AIFunction CreateFunction() => AgentToolFunction.Create(
        this,
        AddAsync,
        ToolNames.AddTimelineNote,
        "Leave a note, status update or progress report on the user's timeline - on a task (taskId), a day (date) or a " +
        "month (month); without any of them it goes on the current month. Use it to report what you reviewed or did.");

    public async Task<string> AddAsync(
        [Description("The note text (markdown allowed).")] string content,
        [Description("note, status update or progress report (default note).")] string? kind = null,
        [Description($"Put the note on this task (id from {ToolNames.ListTimelineTasks}).")] string? taskId = null,
        [Description("Put the note on this day, YYYY-MM-DD.")] string? date = null,
        [Description("Put the note on this month, YYYY-MM.")] string? month = null,
        CancellationToken ct = default)
    {
        if (pdpContext.UserId is not { } userId || userId == Guid.Empty)
        {
            return TimelineToolMessages.NoUser;
        }

        var today = TimelineToolMessages.Today(timeProvider);

        if (TimelineToolText.Normalize(content) is not { } text)
        {
            return "Error: The note cannot be empty.";
        }

        if (TimelineToolText.ParseNoteKind(kind) is not { } noteKind)
        {
            return "Error: kind must be one of: note, status update, progress report.";
        }

        Guid? task = null;
        if (TimelineToolText.Normalize(taskId) is { } taskText)
        {
            if (!Guid.TryParse(taskText, out var parsedTask))
            {
                return $"Error: taskId must be a task id from {ToolNames.ListTimelineTasks}.";
            }

            task = parsedTask;
        }

        TimelinePlacement? placement = null;
        if (task is null)
        {
            placement = TimelinePlacement.Parse(date, month, today, out var error);
            if (error is not null)
            {
                return error;
            }
        }

        try
        {
            var input = new TimelineNoteInput(noteKind, text, task, placement?.Year, placement?.Month, placement?.Date);
            var result = await timeline.AddNoteAsync(userId, AgentToolFunction.CurrentAgentName, input, ct);
            if (!result.IsSuccess)
            {
                return TimelineWriteResults.Describe(result);
            }

            var receipt = result.Value!;
            var target = receipt.TaskId is not null
                ? "the task"
                : new TimelinePlacement(receipt.Year, receipt.Month, receipt.Date).Describe();

            return $"Added a {noteKind switch { TimelineNoteKind.StatusUpdate => "status update", TimelineNoteKind.ProgressReport => "progress report", _ => "note" }} to {target} on the user's timeline.";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Timeline note failed");
            return TimelineToolMessages.WriteFailed;
        }
    }
}
