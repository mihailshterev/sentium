using System.ComponentModel;
using Sentium.Core.Results;
using Microsoft.Extensions.AI;
using Sentium.Praxis.Contracts;
using Sentium.Tempus.Contracts;
using Sentium.Tempus.Contracts.Models;

namespace Sentium.Praxis.Features.Tools.Timeline;

/// <summary>
/// Suggests a new Calendar Event - something that happens at its date and is never ticked. It appears as a pending
/// suggestion the user accepts or rejects; nothing is added until then. Not approval-gated, so workflows can use it.
/// </summary>
[AgentToolPolicy(RiskLevel = ToolRiskLevel.Low, Resource = ToolResource.Timeline, Action = ToolAction.Write)]
internal sealed class ProposeTimelineEventTool(
    ITimelineAgentAccess timeline,
    IPdpContextAccessor pdpContext,
    TimeProvider timeProvider,
    ILogger<ProposeTimelineEventTool> logger) : IAgentTool
{
    public AIFunction CreateFunction()
    {
        return AgentToolFunction.Create(
            this,
            ProposeAsync,
            ToolNames.ProposeTimelineEvent,
            "Suggest a calendar event for the user's timeline - something that simply happens at its date (a birthday, a " +
            "concert, a trip), never ticked as done. Put it on a day (date, optional time and endDate) or pin it to a month " +
            "(month). The user sees it as a suggestion and decides whether to add it. For work the user must do, use " +
            $"{ToolNames.ProposeTimelineTask} instead.");
    }

    public async Task<string> ProposeAsync(
        [Description("Short event title, e.g. 'Maria's birthday'.")] string title,
        [Description("The day of the event, YYYY-MM-DD. Leave empty to pin it to a month instead.")] string? date = null,
        [Description("The month to pin the event to when it has no day yet, YYYY-MM.")] string? month = null,
        [Description("Time of day, HH:mm (24-hour), only with a date.")] string? time = null,
        [Description("Last day of a multi-day event, YYYY-MM-DD, on or after the date.")] string? endDate = null,
        [Description("Comma-separated tags, e.g. 'family, travel'.")] string? tags = null,
        [Description("Optional details for the event.")] string? description = null,
        [Description("One sentence on why you suggest this, shown to the user.")] string? rationale = null,
        CancellationToken ct = default)
    {
        if (pdpContext.UserId is not { } userId || userId == Guid.Empty)
        {
            return TimelineToolMessages.NoUser;
        }

        var today = TimelineToolMessages.Today(timeProvider);

        if (TimelineToolText.Normalize(title) is not { } eventTitle)
        {
            return "Error: Give the event a title.";
        }

        if (TimelinePlacement.Parse(date, month, today, out var error) is not { } placement)
        {
            return error ?? $"Error: Give a date (YYYY-MM-DD) or a month (YYYY-MM). Today is {TimelineToolText.Format(today)}.";
        }

        if (ParseTiming(time, endDate, today, out var startTime, out var end) is { } timingError)
        {
            return timingError;
        }

        try
        {
            var proposal = new TimelineEventProposal(
                eventTitle,
                TimelineToolText.Normalize(description),
                placement.Year,
                placement.Month,
                placement.Date,
                startTime,
                end,
                TimelineToolText.SplitTags(tags),
                TimelineToolText.Normalize(rationale));

            var result = await timeline.ProposeEventAsync(userId, AgentToolFunction.CurrentAgentName, proposal, ct);
            return Describe(result, eventTitle, placement);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Timeline event proposal failed");
            return TimelineToolMessages.WriteFailed;
        }
    }

    /// <returns>An error for the model, or <see langword="null"/>.</returns>
    private static string? ParseTiming(string? time, string? endDate, DateOnly today, out TimeOnly? startTime, out DateOnly? end)
    {
        startTime = null;
        end = null;

        if (TimelineToolText.Normalize(time) is not null)
        {
            if (!TimelineToolText.TryParseTime(time, out var parsedTime))
            {
                return "Error: time must be HH:mm, for example 18:30.";
            }

            startTime = parsedTime;
        }

        if (TimelineToolText.Normalize(endDate) is not null)
        {
            if (!TimelineToolText.TryParseDate(endDate, today, out var parsedEnd))
            {
                return TimelineToolText.DateError("endDate", today);
            }

            end = parsedEnd;
        }

        return null;
    }

    private static string Describe(Result<TimelineProposalReceipt> result, string title, TimelinePlacement placement)
    {
        if (!result.IsSuccess)
        {
            return TimelineWriteResults.Describe(result);
        }

        return result.Value!.AlreadyPending
            ? $"The same event suggestion ('{TimelineToolFormatter.Title(title)}' on {placement.Describe()}) is already waiting for the user's review."
            : $"Suggested the event '{TimelineToolFormatter.Title(title)}' for {placement.Describe()}. It is a proposal awaiting the user's review - the event is not on the timeline until they accept it.";
    }
}
