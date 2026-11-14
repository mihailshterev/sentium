using System.ComponentModel;
using Microsoft.Extensions.AI;
using Sentium.Praxis.Contracts;
using Sentium.Tempus.Contracts;
using Sentium.Tempus.Contracts.Models;

namespace Sentium.Praxis.Features.Tools.Timeline;

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
    public AIFunction CreateFunction()
    {
        return AgentToolFunction.Create(
            this,
            AddAsync,
            ToolNames.AddTimelineNote,
            "Leave a note, status update or progress report on the user's timeline - on a task (taskId), a day (date) or a " +
            "month (month); without any of them it goes on the current month. Use it to report what you reviewed or did.");
    }

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
