using Microsoft.EntityFrameworkCore;
using Sentium.Tempus.Contracts.Models;
using Sentium.Tempus.Data;
using Sentium.Tempus.Dtos;
using Sentium.Tempus.Features.Tasks;

namespace Sentium.Tempus.Features.Overview;

public interface ITimelineOverviewService
{
    /// <param name="clientToday">The caller's local date, used for overdue counts when it is plausible.</param>
    Task<YearOverviewDto> GetYearAsync(int year, DateOnly? clientToday, CancellationToken ct = default);

    Task<MonthDetailDto> GetMonthAsync(int year, int month, CancellationToken ct = default);
}

public sealed class TimelineOverviewService(TempusDbContext db, TimeProvider timeProvider) : ITimelineOverviewService
{
    public async Task<YearOverviewDto> GetYearAsync(int year, DateOnly? clientToday, CancellationToken ct = default)
    {
        var today = TimelineCalendar.ResolveToday(clientToday, TimelineCalendar.UtcToday(timeProvider));

        // Overdue needs each task's month end, so tasks are aggregated in memory (a year of one user's tasks).
        var tasks = await db.TimelineTasks
            .AsNoTracking()
            .Where(t => t.Year == year)
            .Select(t => new { t.Month, t.Date, t.DueDate, t.Status, Assigned = t.AssignedAgent != null })
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

        return new YearOverviewDto(year, today, months);
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
