using Sentium.Core.Results;
using Sentium.Tempus.Contracts.Models;
using Sentium.Tempus.Entities;
using Sentium.Tempus.Features.Notes;
using Sentium.Tempus.Features.Proposals;
using Sentium.Tempus.Features.Tasks;

namespace Sentium.Tempus.Common;

internal static class TimelineMapping
{
    /// <param name="series">The Entry's Series, when it is an occurrence.</param>
    /// <param name="today">Today in the user's time zone, for <see cref="TimelineTaskDto.IsPast"/>.</param>
    public static TimelineTaskDto ToDto(TimelineTask task, int noteCount, TimelineSeries? series, DateOnly today)
    {
        return new(
            task.Id,
            task.Kind,
            task.Title,
            task.Description,
            task.Year,
            task.Month,
            task.Date,
            task.StartTime,
            task.EndDate,
            task.DueDate,
            task.Priority,
            task.Status,
            task.CompletedAt,
            task.Tags,
            task.AssignedAgent,
            task.Origin,
            task.CreatedByAgent,
            noteCount,
            task.SeriesId,
            task.OccurrenceDate,
            task.IsDetached,
            series is null ? null : ToRepeat(series),
            task.ReminderOffsetMinutes,
            IsPast(task, today),
            task.CreatedAt,
            task.UpdatedAt);
    }

    public static TimelineRepeat ToRepeat(TimelineSeries series)
        => new(series.Frequency, series.Interval, series.Weekdays.Count == 0 ? null : series.Weekdays, series.Until);

    public static bool IsPast(TimelineTask task, DateOnly today)
        => task.Kind == TimelineEntryKind.CalendarEvent && TimelineCalendar.IsPast(task.Date, task.EndDate, task.Year, task.Month, today);

    public static TimelineNoteDto ToDto(TimelineNote note)
    {
        return new(
            note.Id,
            note.Year,
            note.Month,
            note.Date,
            note.TaskId,
            note.Kind,
            note.Author,
            note.AuthorName,
            note.Content,
            note.CreatedAt);
    }

    public static TimelineProposalDto ToDto(TimelineProposal proposal)
    {
        return new(
            proposal.Id,
            proposal.Kind,
            proposal.Status,
            proposal.TaskId,
            proposal.Title,
            proposal.Description,
            proposal.Priority,
            proposal.Tags,
            proposal.AssignedAgent,
            proposal.DueDate,
            proposal.StartTime,
            proposal.EndDate,
            proposal.Year,
            proposal.Month,
            proposal.Date,
            proposal.FromYear,
            proposal.FromMonth,
            proposal.FromDate,
            proposal.Rationale,
            proposal.ProposedBy,
            proposal.CreatedAt,
            proposal.ResolvedAt);
    }

    public static TimelineTaskInfo ToInfo(TimelineTask task, DateOnly today)
    {
        return new(
            task.Id,
            task.Title,
            task.Year,
            task.Month,
            task.Date,
            task.DueDate,
            task.Priority,
            task.Status,
            task.Tags,
            task.AssignedAgent,
            task.Kind,
            task.StartTime,
            task.EndDate,
            IsPast(task, today),
            task.SeriesId is not null);
    }

    public static TimelineNoteInfo ToInfo(TimelineNote note)
    {
        return new(
            note.Id,
            note.Kind,
            note.Author,
            note.AuthorName,
            note.Content,
            note.CreatedAt);
    }

    /// <summary>
    /// Re-types a non-success result (its status and error carry over).
    /// </summary>
    public static Result<TOut> Propagate<TIn, TOut>(this Result<TIn> result)
    {
        return result.Status switch
        {
            ResultStatus.NotFound => Result<TOut>.NotFound(result.Error),
            ResultStatus.Conflict => Result<TOut>.Conflict(result.Error ?? "Conflict."),
            ResultStatus.Invalid => Result<TOut>.Invalid(result.Error ?? "Invalid request."),
            _ => throw new InvalidOperationException("Only non-success results can be propagated.")
        };
    }
}
