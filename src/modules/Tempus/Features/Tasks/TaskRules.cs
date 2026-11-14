using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Sentium.Tempus.Common;
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
    {
        return dueDate is { } due && !TempusLimits.IsValidYear(due.Year)
            ? string.Create(CultureInfo.InvariantCulture, $"The due date must be between {TempusLimits.MinYear} and {TempusLimits.MaxYear}.")
            : null;
    }

    /// <summary>
    /// The kind-specific rules of a saved Entry: Task-only and Calendar Event-only fields, the end day and time, the
    /// Series rule and the Reminder offset.
    /// </summary>
    /// <returns>An error message, or <see langword="null"/> when the Entry is valid.</returns>
    public static string? ValidateEntry(SaveTimelineTaskRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return ValidateKindFields(request)
            ?? ValidateTiming(request.Date, request.StartTime, request.EndDate)
            ?? ValidateRepeat(request.Repeat, request.Year, request.Month, request.Date)
            ?? ValidateReminder(request.ReminderOffsetMinutes, request.Date, request.StartTime);
    }

    public static string? ValidateTiming(DateOnly? date, TimeOnly? startTime, DateOnly? endDate)
    {
        if (startTime is not null && date is null)
        {
            return "A time needs a day.";
        }

        if (endDate is not { } end)
        {
            return null;
        }

        if (date is not { } day)
        {
            return "An end day needs a day.";
        }

        return end < day ? "The end day cannot be before the day."
            : !TempusLimits.IsValidYear(end.Year) ? string.Create(CultureInfo.InvariantCulture, $"The end day must be between {TempusLimits.MinYear} and {TempusLimits.MaxYear}.")
            : null;
    }

    public static string? ValidateRepeat(TimelineRepeat? repeat, int year, int month, DateOnly? date)
    {
        if (repeat is null)
        {
            return null;
        }

        if (!Enum.IsDefined(repeat.Frequency))
        {
            return "Unknown repeat frequency.";
        }

        if (repeat.Interval is < 1 or > TempusLimits.MaxRepeatInterval)
        {
            return string.Create(CultureInfo.InvariantCulture, $"The repeat interval must be between 1 and {TempusLimits.MaxRepeatInterval}.");
        }

        if (date is null && repeat.Frequency is not (TimelineRepeatFrequency.Monthly or TimelineRepeatFrequency.Yearly))
        {
            return "An Entry pinned to a month can only repeat monthly or yearly.";
        }

        if (repeat.Weekdays is { Count: > 0 } weekdays
            && (repeat.Frequency != TimelineRepeatFrequency.Weekly || weekdays.Any(d => !Enum.IsDefined(d))))
        {
            return "Weekdays can only be chosen for a weekly repeat.";
        }

        if (repeat.Until is { } until
            && TempusLimits.IsValidMonth(year, month)
            && (until < (date ?? TimelineCalendar.FirstDay(year, month)) || !TempusLimits.IsValidYear(until.Year)))
        {
            return "The repeat must end on or after the first occurrence, and by the end of the supported years.";
        }

        return null;
    }

    public static string? ValidateReminder(int? offsetMinutes, DateOnly? date, TimeOnly? startTime)
    {
        if (offsetMinutes is not { } offset)
        {
            return null;
        }

        if (date is not null && startTime is null)
        {
            return offset is ReminderSchedule.OnTheDay or ReminderSchedule.DayBefore or ReminderSchedule.WeekBefore
                ? null
                : "An Entry without a time can be reminded on the day, the day before or a week before.";
        }

        return offset is >= 0 and <= ReminderSchedule.WeekBefore ? null : "A Reminder can be at most a week before.";
    }

    /// <summary>
    /// The SQL form of <see cref="TimelineCalendar.IsOverdue"/>, for Tasks only: an open Task whose deadline (due
    /// date, else day, else month end) is before today. Calendar Events are never overdue.
    /// </summary>
    public static IQueryable<TimelineTask> Overdue(IQueryable<TimelineTask> tasks, DateOnly today)
    {
        var currentMonth = TimelineCalendar.MonthIndex(today.Year, today.Month);
        return tasks.Where(t => t.Kind == TimelineEntryKind.Task
            && t.Status != TimelineTaskStatus.Done
            && ((t.DueDate != null && t.DueDate < today)
                || (t.DueDate == null && t.Date != null && t.Date < today)
                || (t.DueDate == null && t.Date == null && (t.Year * 12) + t.Month - 1 < currentMonth)));
    }

    public static bool IsAt(TimelineTask task, int year, int month, DateOnly? date)
        => task.Year == year && task.Month == month && task.Date == date;

    /// <summary>
    /// Computes the Reminder's due time and re-arms it (a moved Entry reminds again).
    /// </summary>
    public static void ArmReminder(TimelineTask task, UserCalendar calendar)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(calendar);

        task.ReminderDueAt = calendar.ReminderDueAt(task.Date, task.StartTime, task.Year, task.Month, task.ReminderOffsetMinutes);
        task.ReminderSentAt = null;
    }

    /// <summary>
    /// Puts an Entry on a new day or month: a multi-day Calendar Event keeps its length, an Entry pinned to a month
    /// loses its time and end day, an occurrence is detached from its Series, and the Reminder is re-armed.
    /// </summary>
    public static void MoveTo(TimelineTask task, int year, int month, DateOnly? date, UserCalendar calendar, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(task);

        var length = task is { Date: { } from, EndDate: { } end } ? end.DayNumber - from.DayNumber : (int?)null;

        task.Year = year;
        task.Month = month;
        task.Date = date;
        task.EndDate = date is { } day && length is { } days ? day.AddDays(days) : null;
        task.StartTime = date is null ? null : task.StartTime;
        task.IsDetached = task.SeriesId is not null;
        task.UpdatedAt = now;
        ArmReminder(task, calendar);
    }

    /// <summary>
    /// Not yet over: the last day (end day, else day, else month end) is today or later. The SQL form of
    /// <see cref="TimelineCalendar.IsPast"/>, for Tasks and Calendar Events alike.
    /// </summary>
    public static IQueryable<TimelineTask> NotPast(IQueryable<TimelineTask> tasks, DateOnly today)
    {
        var currentMonth = TimelineCalendar.MonthIndex(today.Year, today.Month);
        return tasks.Where(t => (t.EndDate != null && t.EndDate >= today)
            || (t.EndDate == null && t.Date != null && t.Date >= today)
            || (t.Date == null && (t.Year * 12) + t.Month - 1 >= currentMonth));
    }

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
    {
        return db.TimelineProposals
            .Where(p => p.TaskId == taskId
                && p.Kind == TimelineProposalKind.Reschedule
                && p.Status == TimelineProposalStatus.Pending
                && (keepProposalId == null || p.Id != keepProposalId))
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Status, TimelineProposalStatus.Superseded)
                .SetProperty(p => p.ResolvedAt, now), ct);
    }

    /// <summary>
    /// Open tasks first, then by priority (most urgent first), deadline and age. Done in memory: priorities are
    /// stored by name, so SQL would sort them alphabetically.
    /// </summary>
    public static IOrderedEnumerable<T> OrderForDisplay<T>(IEnumerable<T> items, Func<T, TimelineTask> task)
    {
        return items
            .OrderBy(i => task(i).Status == TimelineTaskStatus.Done)
            .ThenByDescending(i => task(i).Priority)
            .ThenBy(i => task(i).DueDate ?? DateOnly.MaxValue)
            .ThenBy(i => task(i).CreatedAt);
    }

    /// <summary>
    /// Task-only and Calendar Event-only fields. Without a kind (an update that omits it) the rules wait until the
    /// stored kind is known.
    /// </summary>
    private static string? ValidateKindFields(SaveTimelineTaskRequest request)
    {
        if (request.Kind is not { } kind)
        {
            return null;
        }

        if (!Enum.IsDefined(kind))
        {
            return "Unknown kind.";
        }

        if (kind == TimelineEntryKind.Task)
        {
            return request.EndDate is null ? null : "Only a Calendar Event has an end day.";
        }

        return request.Priority is not null || request.Status is not null || request.DueDate is not null || !string.IsNullOrWhiteSpace(request.AssignedAgent)
            ? "A Calendar Event has no status, priority, due date or assigned agent."
            : null;
    }
}
