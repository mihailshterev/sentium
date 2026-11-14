using Microsoft.EntityFrameworkCore;
using Sentium.Core.Text;
using Sentium.Tempus.Common;
using Sentium.Tempus.Contracts.Models;
using Sentium.Tempus.Data;
using Sentium.Tempus.Entities;
using Sentium.Tempus.Features.Tasks;

namespace Sentium.Tempus.Features.Series;

/// <summary>
/// Creates Series and their occurrences. Occurrences are created ahead to a rolling horizon; which dates already
/// exist is read back before inserting, and a unique index on (<c>SeriesId</c>, <c>OccurrenceDate</c>) catches a
/// concurrent run, so materialising twice never duplicates an occurrence.
/// </summary>
internal sealed class TimelineSeriesWriter(TempusDbContext db)
{
    /// <summary>
    /// The last day occurrences are created for: a rolling year ahead, and at least the first occurrence.
    /// </summary>
    public static DateOnly Horizon(DateOnly today, TimelineSeries series)
    {
        ArgumentNullException.ThrowIfNull(series);

        var rolling = today.AddMonths(TempusLimits.SeriesHorizonMonths);
        return rolling > series.StartDate ? rolling : series.StartDate;
    }

    /// <summary>
    /// A new (unsaved) Series from a saved Entry's fields, starting at its placement.
    /// </summary>
    public static TimelineSeries Create(Guid userId, TimelineEntryKind kind, SaveTimelineTaskRequest request, TimelineRepeat repeat, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(repeat);

        var start = request.Date ?? TimelineCalendar.FirstDay(request.Year, request.Month);
        var isTask = kind == TimelineEntryKind.Task;

        return new TimelineSeries
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Kind = kind,
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Tags = TagNormalizer.Normalize(request.Tags),
            Priority = isTask ? request.Priority ?? TimelineTaskPriority.Normal : TimelineTaskPriority.Normal,
            AssignedAgent = isTask && !string.IsNullOrWhiteSpace(request.AssignedAgent) ? request.AssignedAgent.Trim() : null,
            DueOffsetDays = isTask && request.DueDate is { } due ? due.DayNumber - start.DayNumber : null,
            StartTime = request.Date is null ? null : request.StartTime,
            DurationDays = !isTask && request.Date is { } day && request.EndDate is { } end ? end.DayNumber - day.DayNumber : null,
            ReminderOffsetMinutes = request.ReminderOffsetMinutes,
            IsPinned = request.Date is null,
            Frequency = repeat.Frequency,
            Interval = repeat.Interval,
            Weekdays = repeat.Frequency == TimelineRepeatFrequency.Weekly && repeat.Weekdays is { } weekdays ? [.. weekdays.Distinct()] : [],
            StartDate = start,
            Until = repeat.Until,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// Adds the occurrences missing up to <see cref="Horizon"/> and saves.
    /// </summary>
    /// <returns>The number of occurrences added.</returns>
    public async Task<int> MaterializeAsync(TimelineSeries series, UserCalendar calendar, DateTimeOffset now, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(calendar);

        var through = Horizon(calendar.Today(now), series);
        var from = series.MaterializedThrough is { } done ? done.AddDays(1) : series.StartDate;
        var excluded = series.ExcludedDates.ToHashSet();

        var dates = SeriesExpander.Occurrences(TimelineMapping.ToRepeat(series), series.StartDate, from, through)
            .Where(d => !excluded.Contains(d))
            .ToList();

        var added = 0;
        if (dates.Count > 0)
        {
            var existing = (await db.TimelineTasks
                    .Where(t => t.SeriesId == series.Id && t.OccurrenceDate != null && dates.Contains(t.OccurrenceDate.Value))
                    .Select(t => t.OccurrenceDate!.Value)
                    .ToListAsync(ct))
                .ToHashSet();

            foreach (var date in dates.Where(d => !existing.Contains(d)))
            {
                db.TimelineTasks.Add(NewOccurrence(series, date, calendar, now));
                added++;
            }
        }

        if (series.MaterializedThrough is null || series.MaterializedThrough < through)
        {
            series.MaterializedThrough = through;
        }

        await db.SaveChangesAsync(ct);
        return added;
    }

    public static TimelineTask NewOccurrence(TimelineSeries series, DateOnly occurrence, UserCalendar calendar, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(calendar);

        DateOnly? date = series.IsPinned ? null : occurrence;
        var task = new TimelineTask
        {
            Id = Guid.CreateVersion7(),
            UserId = series.UserId,
            Kind = series.Kind,
            Title = series.Title,
            Description = series.Description,
            Year = occurrence.Year,
            Month = occurrence.Month,
            Date = date,
            StartTime = date is null ? null : series.StartTime,
            EndDate = date is { } day && series.DurationDays is { } days ? day.AddDays(days) : null,
            DueDate = series.DueOffsetDays is { } offset ? occurrence.AddDays(offset) : null,
            Priority = series.Priority,
            Status = TimelineTaskStatus.Todo,
            Tags = [.. series.Tags],
            AssignedAgent = series.AssignedAgent,
            Origin = TimelineAuthorKind.User,
            SeriesId = series.Id,
            OccurrenceDate = occurrence,
            ReminderOffsetMinutes = series.ReminderOffsetMinutes,
            CreatedAt = now,
            UpdatedAt = now
        };

        TaskRules.ArmReminder(task, calendar);
        return task;
    }
}
