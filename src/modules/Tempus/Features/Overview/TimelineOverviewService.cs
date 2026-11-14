using Microsoft.EntityFrameworkCore;
using Sentium.Infrastructure.Security;
using Sentium.Tempus.Common;
using Sentium.Tempus.Contracts.Models;
using Sentium.Tempus.Data;
using Sentium.Tempus.Features.Calendar;
using Sentium.Tempus.Features.Tasks;

namespace Sentium.Tempus.Features.Overview;

internal sealed class TimelineOverviewService(
    TempusDbContext db,
    TimeProvider timeProvider,
    ICurrentUser currentUser,
    IUserCalendarProvider calendars) : ITimelineOverviewService
{
    /// <summary>
    /// Entries listed per Up next column; the overdue total is reported separately.
    /// </summary>
    private const int UpcomingListMax = 12;

    public async Task<YearOverviewDto> GetYearAsync(int year, DateOnly? clientToday, CancellationToken ct = default)
    {
        var today = await TodayAsync(clientToday, ct);
        var firstDay = TimelineCalendar.FirstDay(year, 1);
        var lastDay = TimelineCalendar.LastDay(year, 12);

        // Overdue needs each task's month end, so Entries are aggregated in memory (a year of one user's Entries).
        // A multi-day Calendar Event that started last year still ticks this year's days.
        var entries = await db.TimelineTasks
            .AsNoTracking()
            .Where(t => t.Year == year || (t.EndDate != null && t.Date < firstDay && t.EndDate >= firstDay))
            .Select(t => new { t.Kind, t.Year, t.Month, t.Date, t.EndDate, t.DueDate, t.Status, t.Priority, Assigned = t.AssignedAgent != null })
            .ToListAsync(ct);

        var tasks = entries.Where(t => t.Kind == TimelineEntryKind.Task).ToList();
        var events = entries.Where(t => t.Kind == TimelineEntryKind.CalendarEvent).ToList();

        var notes = await db.TimelineNotes
            .AsNoTracking()
            .Where(n => n.Year == year)
            .GroupBy(n => n.Month)
            .Select(g => new
            {
                Month = g.Key,
                Count = g.Count(),
                AgentCount = g.Sum(n => n.Author == TimelineAuthorKind.Agent ? 1 : 0),
                LastAgentAt = g.Max(n => n.Author == TimelineAuthorKind.Agent ? (DateTimeOffset?)n.CreatedAt : null)
            })
            .ToDictionaryAsync(g => g.Month, ct);

        var proposals = await db.TimelineProposals
            .AsNoTracking()
            .Where(p => p.Year == year && p.Status == TimelineProposalStatus.Pending)
            .GroupBy(p => p.Month)
            .Select(g => new { Month = g.Key, Count = g.Count(), LastAt = g.Max(p => (DateTimeOffset?)p.CreatedAt) })
            .ToDictionaryAsync(g => g.Month, ct);

        var months = Enumerable.Range(1, 12).Select(month =>
        {
            var inMonth = tasks.Where(t => t.Month == month).ToList();
            var noteStats = notes.GetValueOrDefault(month);
            var proposalStats = proposals.GetValueOrDefault(month);

            return new MonthSummaryDto(
                month,
                inMonth.Count,
                inMonth.Count(t => t.Status != TimelineTaskStatus.Done),
                inMonth.Count(t => t.Status == TimelineTaskStatus.Done),
                inMonth.Count(t => TimelineCalendar.IsOverdue(t.Status, t.DueDate, t.Date, year, month, today)),
                inMonth.Count(t => t.Assigned),
                noteStats?.Count ?? 0,
                noteStats?.AgentCount ?? 0,
                proposalStats?.Count ?? 0,
                Latest(noteStats?.LastAgentAt, proposalStats?.LastAt),
                events.Count(e => e.Year == year && e.Month == month));
        }).ToList();

        var eventDays = events
            .Where(e => e.Date is not null)
            .SelectMany(e => DaysOf(e.Date!.Value, e.EndDate, firstDay, lastDay))
            .GroupBy(d => d)
            .ToDictionary(g => g.Key, g => g.Count());

        var taskDays = tasks
            .Where(t => t.Date is not null)
            .GroupBy(t => t.Date!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var days = taskDays.Keys
            .Union(eventDays.Keys)
            .Order()
            .Select(day =>
            {
                var onDay = taskDays.GetValueOrDefault(day) ?? [];
                return new DaySummaryDto(
                    day,
                    onDay.Count(t => t.Status != TimelineTaskStatus.Done),
                    onDay.Count(t => t.Status == TimelineTaskStatus.Done),
                    onDay.Any(t => t.Status != TimelineTaskStatus.Done
                        && (t.Priority >= TimelineTaskPriority.High
                            || TimelineCalendar.IsOverdue(t.Status, t.DueDate, t.Date, year, t.Month, today))),
                    eventDays.GetValueOrDefault(day));
            })
            .ToList();

        return new YearOverviewDto(year, today, months, days);
    }

    public async Task<UpcomingDto> GetUpcomingAsync(DateOnly? clientToday, int days, CancellationToken ct = default)
    {
        var today = await TodayAsync(clientToday, ct);
        var window = Math.Clamp(days, 1, TempusLimits.MaxUpcomingDays);
        var lastDay = today.AddDays(window - 1);

        var overdueQuery = TaskRules.Overdue(db.TimelineTasks.AsNoTracking(), today);
        var overdueCount = await overdueQuery.CountAsync(ct);

        // Oldest months first in SQL, then exact deadline order in memory.
        var overdue = (await TimelineTaskRow.Select(overdueQuery
                    .OrderBy(t => t.Year)
                    .ThenBy(t => t.Month)
                    .Take(TempusLimits.MaxListedTasks))
                .ToListAsync(ct))
            .OrderBy(r => TimelineCalendar.EffectiveDeadline(r.Task.DueDate, r.Task.Date, r.Task.Year, r.Task.Month))
            .ThenByDescending(r => r.Task.Priority)
            .ThenBy(r => r.Task.CreatedAt)
            .Take(UpcomingListMax)
            .Select(r => ToDto(r, today))
            .ToList();

        // Day order from SQL; priority within a day in memory (priorities are stored by name).
        var next = (await TimelineTaskRow.Select(db.TimelineTasks
                    .AsNoTracking()
                    .Where(t => t.Kind == TimelineEntryKind.Task
                        && t.Status != TimelineTaskStatus.Done
                        && t.Date != null && t.Date >= today && t.Date <= lastDay)
                    .OrderBy(t => t.Date)
                    .Take(TempusLimits.MaxListedTasks))
                .ToListAsync(ct))
            .GroupBy(r => r.Task.Date)
            .SelectMany(g => TaskRules.OrderForDisplay(g, r => r.Task))
            .Take(UpcomingListMax)
            .Select(r => ToDto(r, today))
            .ToList();

        var events = (await TimelineTaskRow.Select(db.TimelineTasks
                    .AsNoTracking()
                    .Where(t => t.Kind == TimelineEntryKind.CalendarEvent
                        && t.Date != null && t.Date <= lastDay && (t.EndDate ?? t.Date) >= today)
                    .OrderBy(t => t.Date)
                    .ThenBy(t => t.StartTime)
                    .Take(UpcomingListMax))
                .ToListAsync(ct))
            .Select(r => ToDto(r, today))
            .ToList();

        var pending = await db.TimelineProposals.CountAsync(p => p.Status == TimelineProposalStatus.Pending, ct);

        return new UpcomingDto(today, window, overdue, overdueCount, next, pending, events);
    }

    public async Task<MonthDetailDto> GetMonthAsync(int year, int month, DateOnly? clientToday = null, CancellationToken ct = default)
    {
        var today = await TodayAsync(clientToday, ct);

        var tasks = await TimelineTaskRow.Select(db.TimelineTasks
                .AsNoTracking()
                .Where(t => t.Year == year && t.Month == month))
            .ToListAsync(ct);

        var notes = await db.TimelineNotes
            .AsNoTracking()
            .Where(n => n.Year == year && n.Month == month)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(ct);

        var proposals = await db.TimelineProposals
            .AsNoTracking()
            .Where(p => p.Status == TimelineProposalStatus.Pending
                && ((p.Year == year && p.Month == month) || (p.FromYear == year && p.FromMonth == month)))
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(ct);

        return new MonthDetailDto(
            year,
            month,
            [.. TaskRules.OrderForDisplay(tasks, r => r.Task).Select(r => ToDto(r, today))],
            [.. notes.Select(TimelineMapping.ToDto)],
            [.. proposals.Select(TimelineMapping.ToDto)]);
    }

    private async Task<DateOnly> TodayAsync(DateOnly? clientToday, CancellationToken ct)
    {
        var calendar = await calendars.GetAsync(currentUser.UserId ?? Guid.Empty, ct);
        return TimelineCalendar.ResolveToday(clientToday, calendar.Today(timeProvider.GetUtcNow()));
    }

    private static TimelineTaskDto ToDto(TimelineTaskRow row, DateOnly today)
        => TimelineMapping.ToDto(row.Task, row.NoteCount, row.Series, today);

    /// <summary>
    /// The days an Entry covers (its day through its end day), clipped to the overview's year.
    /// </summary>
    private static IEnumerable<DateOnly> DaysOf(DateOnly date, DateOnly? endDate, DateOnly firstDay, DateOnly lastDay)
    {
        var from = date < firstDay ? firstDay : date;
        var to = endDate is { } end && end < lastDay ? end : endDate is null ? date : lastDay;

        for (var day = from; day <= to; day = day.AddDays(1))
        {
            yield return day;
        }
    }

    private static DateTimeOffset? Latest(DateTimeOffset? a, DateTimeOffset? b)
        => a is null ? b : b is null ? a : a > b ? a : b;
}
