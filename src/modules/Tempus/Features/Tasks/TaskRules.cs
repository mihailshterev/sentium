using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Sentium.Tempus.Contracts.Models;
using Sentium.Tempus.Data;
using Sentium.Tempus.Entities;

namespace Sentium.Tempus.Features.Tasks;

/// <summary>
/// Placement checks and the bookkeeping every task move needs, shared by the HTTP services and the agent facade.
/// </summary>
internal static class TaskRules
{
    public static string Format(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string FormatMonth(int year, int month) => string.Create(CultureInfo.InvariantCulture, $"{year:D4}-{month:D2}");

    /// <returns>An error message, or <see langword="null"/> when the placement is valid.</returns>
    public static string? ValidatePlacement(int year, int month, DateOnly? date)
    {
        if (!TempusLimits.IsValidMonth(year, month))
        {
            return string.Create(CultureInfo.InvariantCulture, $"The month must be between {TempusLimits.MinYear}-01 and {TempusLimits.MaxYear}-12.");
        }

        if (date is { } day && !TimelineCalendar.IsInMonth(day, year, month))
        {
            return $"The day {Format(day)} is not in {FormatMonth(year, month)}.";
        }

        return null;
    }

    public static string? ValidateDueDate(DateOnly? dueDate)
        => dueDate is { } due && !TempusLimits.IsValidYear(due.Year)
            ? string.Create(CultureInfo.InvariantCulture, $"The due date must be between {TempusLimits.MinYear} and {TempusLimits.MaxYear}.")
            : null;

    public static bool IsAt(TimelineTask task, int year, int month, DateOnly? date)
        => task.Year == year && task.Month == month && task.Date == date;

    /// <summary>
    /// After a task moved: its notes follow it, and the moves agents proposed for its old place are superseded
    /// (except <paramref name="keepProposalId"/>, the proposal being accepted).
    /// </summary>
    public static async Task AfterMoveAsync(TempusDbContext db, TimelineTask task, DateTimeOffset now, Guid? keepProposalId, CancellationToken ct)
    {
        await db.TimelineNotes
            .Where(n => n.TaskId == task.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(n => n.Year, task.Year)
                .SetProperty(n => n.Month, task.Month)
                .SetProperty(n => n.Date, task.Date), ct);

        await SupersedePendingReschedulesAsync(db, task.Id, now, keepProposalId, ct);
    }

    public static Task<int> SupersedePendingReschedulesAsync(TempusDbContext db, Guid taskId, DateTimeOffset now, Guid? keepProposalId, CancellationToken ct)
        => db.TimelineProposals
            .Where(p => p.TaskId == taskId
                && p.Kind == TimelineProposalKind.Reschedule
                && p.Status == TimelineProposalStatus.Pending
                && (keepProposalId == null || p.Id != keepProposalId))
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Status, TimelineProposalStatus.Superseded)
                .SetProperty(p => p.ResolvedAt, now), ct);

    /// <summary>
    /// Open tasks first, then by priority (most urgent first), deadline and age. Done in memory: priorities are
    /// stored by name, so SQL would sort them alphabetically.
    /// </summary>
    public static IOrderedEnumerable<T> OrderForDisplay<T>(IEnumerable<T> items, Func<T, TimelineTask> task)
        => items
            .OrderBy(i => task(i).Status == TimelineTaskStatus.Done)
            .ThenByDescending(i => task(i).Priority)
            .ThenBy(i => task(i).DueDate ?? DateOnly.MaxValue)
            .ThenBy(i => task(i).CreatedAt);
}
