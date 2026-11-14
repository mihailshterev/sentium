using Microsoft.EntityFrameworkCore;
using Sentium.Tempus.Common;
using Sentium.Tempus.Contracts.Models;
using Sentium.Tempus.Data;
using Sentium.Tempus.Features.Tasks;

namespace Sentium.Tempus.Features.Overview;

public sealed class TimelineOverviewService(TempusDbContext db, TimeProvider timeProvider) : ITimelineOverviewService
{
    /// <summary>
    /// Tasks listed per Up next column; the overdue total is reported separately.
    /// </summary>
    private const int UpcomingListMax = 12;

    public async Task<YearOverviewDto> GetYearAsync(int year, DateOnly? clientToday, CancellationToken ct = default)
    {
        var today = TimelineCalendar.ResolveToday(clientToday, TimelineCalendar.UtcToday(timeProvider));

        // Overdue needs each task's month end, so tasks are aggregated in memory (a year of one user's tasks).
        var tasks = await db.TimelineTasks
            .AsNoTracking()
            .Where(t => t.Year == year)
            .Select(t => new { t.Month, t.Date, t.DueDate, t.Status, t.Priority, Assigned = t.AssignedAgent != null })
            .ToListAsync(ct);

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
                Latest(noteStats?.LastAgentAt, proposalStats?.LastAt));
        }).ToList();

        var days = tasks
            .Where(t => t.Date is not null)
            .GroupBy(t => t.Date!.Value)
            .OrderBy(g => g.Key)
            .Select(g => new DaySummaryDto(
                g.Key,
                g.Count(t => t.Status != TimelineTaskStatus.Done),
                g.Count(t => t.Status == TimelineTaskStatus.Done),
                g.Any(t => t.Status != TimelineTaskStatus.Done
                    && (t.Priority >= TimelineTaskPriority.High
                        || TimelineCalendar.IsOverdue(t.Status, t.DueDate, t.Date, year, t.Month, today)))))
            .ToList();

        return new YearOverviewDto(year, today, months, days);
    }

    public async Task<UpcomingDto> GetUpcomingAsync(DateOnly? clientToday, int days, CancellationToken ct = default)
    {
        var today = TimelineCalendar.ResolveToday(clientToday, TimelineCalendar.UtcToday(timeProvider));
        var window = Math.Clamp(days, 1, TempusLimits.MaxUpcomingDays);
        var lastDay = today.AddDays(window - 1);
        var currentMonth = TimelineCalendar.MonthIndex(today.Year, today.Month);

        // The SQL form of TimelineCalendar.IsOverdue: the deadline is the due date, else the day, else the month end.
        var overdueQuery = db.TimelineTasks
            .AsNoTracking()
            .Where(t => t.Status != TimelineTaskStatus.Done
                && ((t.DueDate != null && t.DueDate < today)
                    || (t.DueDate == null && t.Date != null && t.Date < today)
                    || (t.DueDate == null && t.Date == null && (t.Year * 12) + t.Month - 1 < currentMonth)));

        var overdueCount = await overdueQuery.CountAsync(ct);

        // Oldest months first in SQL, then exact deadline order in memory.
        var overdue = (await overdueQuery
                .OrderBy(t => t.Year)
                .ThenBy(t => t.Month)
                .Take(TempusLimits.MaxListedTasks)
                .Select(t => new { Task = t, NoteCount = t.Notes.Count })
                .ToListAsync(ct))
            .OrderBy(r => TimelineCalendar.EffectiveDeadline(r.Task.DueDate, r.Task.Date, r.Task.Year, r.Task.Month))
            .ThenByDescending(r => r.Task.Priority)
            .ThenBy(r => r.Task.CreatedAt)
            .Take(UpcomingListMax)
            .Select(r => TimelineMapping.ToDto(r.Task, r.NoteCount))
            .ToList();

        // Day order from SQL; priority within a day in memory (priorities are stored by name).
        var next = (await db.TimelineTasks
                .AsNoTracking()
                .Where(t => t.Status != TimelineTaskStatus.Done && t.Date != null && t.Date >= today && t.Date <= lastDay)
                .OrderBy(t => t.Date)
                .Take(TempusLimits.MaxListedTasks)
                .Select(t => new { Task = t, NoteCount = t.Notes.Count })
                .ToListAsync(ct))
            .GroupBy(r => r.Task.Date)
            .SelectMany(g => TaskRules.OrderForDisplay(g, r => r.Task))
            .Take(UpcomingListMax)
            .Select(r => TimelineMapping.ToDto(r.Task, r.NoteCount))
            .ToList();

        var pending = await db.TimelineProposals.CountAsync(p => p.Status == TimelineProposalStatus.Pending, ct);

        return new UpcomingDto(today, window, overdue, overdueCount, next, pending);
    }

    public async Task<MonthDetailDto> GetMonthAsync(int year, int month, CancellationToken ct = default)
    {
        var tasks = await db.TimelineTasks
            .AsNoTracking()
            .Where(t => t.Year == year && t.Month == month)
            .Select(t => new { Task = t, NoteCount = t.Notes.Count })
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
            [.. TaskRules.OrderForDisplay(tasks, r => r.Task).Select(r => TimelineMapping.ToDto(r.Task, r.NoteCount))],
            [.. notes.Select(TimelineMapping.ToDto)],
            [.. proposals.Select(TimelineMapping.ToDto)]);
    }

    private static DateTimeOffset? Latest(DateTimeOffset? a, DateTimeOffset? b)
        => a is null ? b : b is null ? a : a > b ? a : b;
}
