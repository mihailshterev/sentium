using System.ComponentModel;
using System.Globalization;
using Microsoft.Extensions.AI;
using Sentium.Praxis.Features.Sentinel;
using Sentium.Praxis.Features.Tools.Attributes;
using Sentium.Tempus.Contracts;
using Sentium.Tempus.Contracts.Models;

namespace Sentium.Praxis.Features.Tools.Timeline;

internal static class TimelineToolMessages
{
    public const string NoUser = "Error: The timeline belongs to a signed-in user, and no user is associated with this run.";
    public const string Unavailable = "Error: The timeline could not be read right now.";
    public const string WriteFailed = "Error: The timeline could not be updated right now.";

    public static DateOnly Today(TimeProvider timeProvider) => DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
}

/// <summary>
/// The user's workload at a glance: the year by month and the coming days by day.
/// </summary>
[AgentToolPolicy(RiskLevel = ToolRiskLevel.Low, Resource = ToolResource.Timeline, Action = ToolAction.Read)]
public sealed class GetTimelineOverviewTool(
    ITimelineAgentAccess timeline,
    IPdpContextAccessor pdpContext,
    TimeProvider timeProvider,
    ILogger<GetTimelineOverviewTool> logger) : IAgentTool
{
    private const int DefaultDays = 14;

    public AIFunction CreateFunction() => AgentToolFunction.Create(
        this,
        GetAsync,
        ToolNames.GetTimelineOverview,
        "Summarise the user's year timeline (their personal planner): open, done and overdue tasks per month, open tasks " +
        "per day for the coming days, and suggestions waiting for their review. Call this first to judge the workload " +
        $"before suggesting or moving tasks; use {ToolNames.ListTimelineTasks} for the tasks themselves.");

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

/// <summary>
/// The tasks planned in a date range, grouped by day, plus the tasks pinned to the months it covers.
/// </summary>
[AgentToolPolicy(RiskLevel = ToolRiskLevel.Low, Resource = ToolResource.Timeline, Action = ToolAction.Search)]
public sealed class ListTimelineTasksTool(
    ITimelineAgentAccess timeline,
    IPdpContextAccessor pdpContext,
    TimeProvider timeProvider,
    ILogger<ListTimelineTasksTool> logger) : IAgentTool
{
    private const int DefaultRangeDays = 30;
    private const int MaxRangeDays = 400;
    private const int DefaultLimit = 50;

    public AIFunction CreateFunction() => AgentToolFunction.Create(
        this,
        ListAsync,
        ToolNames.ListTimelineTasks,
        "List the tasks on the user's timeline between two dates, grouped by day, with priority, status, due date, tags, " +
        "assigned agent and id; tasks pinned to a month without a day are listed per month. Defaults to the next 30 days.");

    public async Task<string> ListAsync(
        [Description("First day, YYYY-MM-DD or 'today' (default: today).")] string? from = null,
        [Description("Last day, YYYY-MM-DD (default: 30 days after 'from').")] string? to = null,
        [Description("Only tasks with this status: todo, in progress or done.")] string? status = null,
        [Description("Only tasks with this tag.")] string? tag = null,
        [Description("Only tasks assigned to this agent, e.g. your own name.")] string? assignedAgent = null,
        [Description("Maximum number of tasks (1-100, default 50).")] int? limit = null,
        CancellationToken ct = default)
    {
        if (pdpContext.UserId is not { } userId || userId == Guid.Empty)
        {
            return TimelineToolMessages.NoUser;
        }

        var today = TimelineToolMessages.Today(timeProvider);

        var start = today;
        if (TimelineToolText.Normalize(from) is not null && !TimelineToolText.TryParseDate(from, today, out start))
        {
            return TimelineToolText.DateError("from", today);
        }

        var end = start.AddDays(DefaultRangeDays);
        if (TimelineToolText.Normalize(to) is not null && !TimelineToolText.TryParseDate(to, today, out end))
        {
            return TimelineToolText.DateError("to", today);
        }

        if (end < start)
        {
            (start, end) = (end, start);
        }

        if (end.DayNumber - start.DayNumber > MaxRangeDays)
        {
            end = start.AddDays(MaxRangeDays);
        }

        TimelineTaskStatus? statusFilter = null;
        if (TimelineToolText.Normalize(status) is not null)
        {
            statusFilter = TimelineToolText.ParseStatus(status);
            if (statusFilter is null)
            {
                return "Error: status must be one of: todo, in progress, done.";
            }
        }

        try
        {
            var query = new TimelineTaskQuery(start, end, statusFilter, TimelineToolText.Normalize(tag), TimelineToolText.Normalize(assignedAgent), IncludePinned: true, limit ?? DefaultLimit);
            var tasks = await timeline.ListTasksAsync(userId, query, ct);
            return TimelineToolFormatter.FormatTasks(tasks, start, end, today);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Timeline task listing failed");
            return TimelineToolMessages.Unavailable;
        }
    }
}

/// <summary>
/// One task in full: description, deadline and the notes left on it.
/// </summary>
[AgentToolPolicy(RiskLevel = ToolRiskLevel.Low, Resource = ToolResource.Timeline, Action = ToolAction.Read)]
public sealed class GetTimelineTaskTool(
    ITimelineAgentAccess timeline,
    IPdpContextAccessor pdpContext,
    TimeProvider timeProvider,
    ILogger<GetTimelineTaskTool> logger) : IAgentTool
{
    public AIFunction CreateFunction() => AgentToolFunction.Create(
        this,
        GetAsync,
        ToolNames.GetTimelineTask,
        "Get one task from the user's timeline in full: description, planned day, due date, priority, status, tags, " +
        $"assigned agent and the notes left on it. Needs the task id from {ToolNames.ListTimelineTasks}.");

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
