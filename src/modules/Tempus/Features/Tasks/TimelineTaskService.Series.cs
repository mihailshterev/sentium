using Microsoft.EntityFrameworkCore;
using Sentium.Tempus.Common;
using Sentium.Tempus.Contracts.Models;
using Sentium.Tempus.Entities;
using Sentium.Tempus.Features.Series;

namespace Sentium.Tempus.Features.Tasks;

internal sealed partial class TimelineTaskService
{
    private async Task<Guid> CreateSeriesAsync(SaveTimelineTaskRequest request, TimelineRepeat repeat, UserCalendar calendar, CancellationToken ct)
    {
        db.ChangeTracker.Clear();

        var series = TimelineSeriesWriter.Create(UserId, request.Kind ?? TimelineEntryKind.Task, request, repeat, timeProvider.GetUtcNow());
        db.TimelineSeries.Add(series);
        await seriesWriter.MaterializeAsync(series, calendar, timeProvider.GetUtcNow(), ct);

        return await db.TimelineTasks
            .Where(t => t.SeriesId == series.Id && t.OccurrenceDate == series.StartDate)
            .Select(t => t.Id)
            .FirstAsync(ct);
    }

    /// <summary>
    /// The Series side of an edit: a one-off Entry given a repeat rule starts a Series; "this one" detaches the
    /// occurrence; "this and all following" ends the old Series the day before and starts a new one from the
    /// edited occurrence (or leaves it one-off when the repeat was removed).
    /// </summary>
    private async Task ApplySeriesEditAsync(TimelineTask task, SaveTimelineTaskRequest request, TimelineEditScope scope, UserCalendar calendar, DateTimeOffset now, CancellationToken ct)
    {
        if (task.SeriesId is not { } seriesId)
        {
            if (request.Repeat is { } repeat)
            {
                await StartSeriesAsync(task, request, repeat, [], calendar, now, ct);
            }

            return;
        }

        if (scope == TimelineEditScope.One)
        {
            task.IsDetached = true;
            return;
        }

        var kept = await EndSeriesBeforeAsync(seriesId, task, calendar.Today(now), includeDetached: false, now, ct);

        task.IsDetached = false;
        if (request.Repeat is { } newRepeat)
        {
            await StartSeriesAsync(task, request, newRepeat, kept, calendar, now, ct);
        }
        else
        {
            task.SeriesId = null;
            task.OccurrenceDate = null;
        }
    }

    private async Task StartSeriesAsync(TimelineTask task, SaveTimelineTaskRequest request, TimelineRepeat repeat, List<DateOnly> excluded, UserCalendar calendar, DateTimeOffset now, CancellationToken ct)
    {
        var series = TimelineSeriesWriter.Create(task.UserId, task.Kind, request, repeat, now);
        series.ExcludedDates = excluded;
        db.TimelineSeries.Add(series);

        task.SeriesId = series.Id;
        task.OccurrenceDate = series.StartDate;
        await db.SaveChangesAsync(ct);

        await seriesWriter.MaterializeAsync(series, calendar, now, ct);
    }

    private async Task<bool> DeleteEntryAsync(Guid id, TimelineEditScope scope, UserCalendar calendar, CancellationToken ct)
    {
        db.ChangeTracker.Clear();

        var task = await db.TimelineTasks.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (task is null)
        {
            return false;
        }

        var now = timeProvider.GetUtcNow();
        if (task is { SeriesId: { } seriesId, OccurrenceDate: { } occurrence })
        {
            if (scope == TimelineEditScope.Following)
            {
                await EndSeriesBeforeAsync(seriesId, task, calendar.Today(now), includeDetached: true, now, ct);
            }
            else if (await db.TimelineSeries.FirstOrDefaultAsync(s => s.Id == seriesId, ct) is { } series)
            {
                series.ExcludedDates = [.. series.ExcludedDates, occurrence];
                series.UpdatedAt = now;
            }
        }

        db.TimelineTasks.Remove(task);
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Ends a Series the day before <paramref name="from"/>'s occurrence and deletes its later occurrences, except
    /// ones already done or past (and, for an edit, ones detached by an earlier "this one" edit).
    /// </summary>
    /// <returns>The occurrence dates of the later occurrences that were kept.</returns>
    private async Task<List<DateOnly>> EndSeriesBeforeAsync(Guid seriesId, TimelineTask from, DateOnly today, bool includeDetached, DateTimeOffset now, CancellationToken ct)
    {
        var split = from.OccurrenceDate ?? from.Date ?? TimelineCalendar.FirstDay(from.Year, from.Month);

        if (await db.TimelineSeries.FirstOrDefaultAsync(s => s.Id == seriesId, ct) is { } series)
        {
            series.Until = split.AddDays(-1);
            series.UpdatedAt = now;
        }

        var later = db.TimelineTasks.Where(t => t.SeriesId == seriesId && t.Id != from.Id && t.OccurrenceDate > split);
        var removable = TaskRules.NotPast(later, today)
            .Where(t => t.Status != TimelineTaskStatus.Done && (includeDetached || !t.IsDetached));

        await removable.ExecuteDeleteAsync(ct);

        return await later.Select(t => t.OccurrenceDate!.Value).ToListAsync(ct);
    }
}
