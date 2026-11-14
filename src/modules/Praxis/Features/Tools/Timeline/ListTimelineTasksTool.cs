using System.ComponentModel;
using Microsoft.Extensions.AI;
using Sentium.Praxis.Contracts;
using Sentium.Tempus.Contracts;
using Sentium.Tempus.Contracts.Models;

namespace Sentium.Praxis.Features.Tools.Timeline;

/// <summary>
/// The Tasks and Calendar Events planned in a date range, grouped by day, plus those pinned to the months it covers.
/// </summary>
[AgentToolPolicy(RiskLevel = ToolRiskLevel.Low, Resource = ToolResource.Timeline, Action = ToolAction.Search)]
internal sealed class ListTimelineTasksTool(
    ITimelineAgentAccess timeline,
    IPdpContextAccessor pdpContext,
    TimeProvider timeProvider,
    ILogger<ListTimelineTasksTool> logger) : IAgentTool
{
    private const int DefaultRangeDays = 30;
    private const int MaxRangeDays = 400;
    private const int DefaultLimit = 50;

    public AIFunction CreateFunction()
    {
        return AgentToolFunction.Create(
            this,
            ListAsync,
            ToolNames.ListTimelineTasks,
            "List the tasks and calendar events on the user's timeline between two dates, grouped by day, with priority, " +
            "status, due date, tags, assigned agent and id; events are marked [Event] (never ticked, 'past' once over). " +
            "Entries pinned to a month without a day are listed per month. Defaults to the next 30 days.");
    }

    public async Task<string> ListAsync(
        [Description("First day, YYYY-MM-DD or 'today' (default: today).")] string? from = null,
        [Description("Last day, YYYY-MM-DD (default: 30 days after 'from').")] string? to = null,
        [Description("Only tasks with this status: todo, in progress or done.")] string? status = null,
        [Description("Only tasks with this tag.")] string? tag = null,
        [Description("Only tasks assigned to this agent, e.g. your own name.")] string? assignedAgent = null,
        [Description("Maximum number of tasks (1-100, default 50).")] int? limit = null,
        [Description("Only 'tasks' or only 'events' (default: both).")] string? kind = null,
        CancellationToken ct = default)
    {
        if (pdpContext.UserId is not { } userId || userId == Guid.Empty)
        {
            return TimelineToolMessages.NoUser;
        }

        var today = TimelineToolMessages.Today(timeProvider);

        if (ParseRange(from, to, today, out var start, out var end) is { } rangeError)
        {
            return rangeError;
        }

        if (ParseFilters(status, kind, out var statusFilter, out var kindFilter) is { } filterError)
        {
            return filterError;
        }

        try
        {
            var query = new TimelineTaskQuery(start, end, statusFilter, TimelineToolText.Normalize(tag), TimelineToolText.Normalize(assignedAgent), IncludePinned: true, limit ?? DefaultLimit, kindFilter);
            var tasks = await timeline.ListTasksAsync(userId, query, ct);
            return TimelineToolFormatter.FormatTasks(tasks, start, end, today);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Timeline task listing failed");
            return TimelineToolMessages.Unavailable;
        }
    }

    /// <summary>
    /// The day range, defaulting to today and the 30 days after; swapped when reversed and capped in length.
    /// </summary>
    /// <returns>An error for the model, or <see langword="null"/>.</returns>
    private static string? ParseRange(string? from, string? to, DateOnly today, out DateOnly start, out DateOnly end)
    {
        start = today;
        end = default;

        if (TimelineToolText.Normalize(from) is not null && !TimelineToolText.TryParseDate(from, today, out start))
        {
            return TimelineToolText.DateError("from", today);
        }

        end = start.AddDays(DefaultRangeDays);
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

        return null;
    }

    /// <returns>An error for the model, or <see langword="null"/>.</returns>
    private static string? ParseFilters(string? status, string? kind, out TimelineTaskStatus? statusFilter, out TimelineEntryKind? kindFilter)
    {
        statusFilter = TimelineToolText.ParseStatus(status);
        kindFilter = TimelineToolText.ParseEntryKind(kind);

        if (TimelineToolText.Normalize(status) is not null && statusFilter is null)
        {
            return "Error: status must be one of: todo, in progress, done.";
        }

        return TimelineToolText.Normalize(kind) is not null && kindFilter is null
            ? "Error: kind must be 'tasks' or 'events'."
            : null;
    }
}
