using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Sentium.Nuntius.Contracts;
using Sentium.Tempus.Common;
using Sentium.Tempus.Contracts.Models;
using Sentium.Tempus.Data;
using Sentium.Tempus.Features.Calendar;
using Sentium.Tempus.Features.Tasks;

namespace Sentium.Tempus.Features.Reminders;

/// <summary>
/// Sends the Reminders that are due. Runs in a system scope across every user, so each row's own user id names the
/// recipient. Each Reminder is claimed with a guarded update before anything is published, so two concurrent sweeps
/// never send it twice. A Reminder due while the system was off still fires unless its Entry is already past (then
/// it is dropped); a Task ticked done before its Reminder never fires.
/// </summary>
internal sealed class TimelineReminderSweep(
    TempusDbContext db,
    IUserCalendarProvider calendars,
    INotifier notifier,
    ILogger<TimelineReminderSweep> logger)
{
    private const int BatchSize = 100;

    /// <returns>The number of Reminders sent.</returns>
    public async Task<int> RunAsync(DateTimeOffset now, CancellationToken ct = default)
    {
        var userCalendars = new Dictionary<Guid, UserCalendar>();
        var sent = 0;

        while (true)
        {
            var due = await Due(now)
                .OrderBy(t => t.ReminderDueAt)
                .Take(BatchSize)
                .Select(t => new DueReminder(t.Id, t.UserId, t.Title, t.Year, t.Month, t.Date, t.StartTime, t.EndDate, t.ReminderDueAt!.Value))
                .ToListAsync(ct);

            foreach (var reminder in due)
            {
                if (!await ClaimAsync(reminder, now, ct))
                {
                    continue;
                }

                if (!userCalendars.TryGetValue(reminder.UserId, out var calendar))
                {
                    calendar = await calendars.GetAsync(reminder.UserId, ct);
                    userCalendars[reminder.UserId] = calendar;
                }

                var today = calendar.Today(now);
                if (TimelineCalendar.IsPast(reminder.Date, reminder.EndDate, reminder.Year, reminder.Month, today))
                {
                    logger.LogInformation("Dropped the Reminder of {EntryId}: the Entry is already past", reminder.Id);
                    continue;
                }

                await notifier.NotifyAsync(NotificationRecipient.User(reminder.UserId), ToMessage(reminder, today), ct);
                sent++;
            }

            if (due.Count < BatchSize)
            {
                return sent;
            }
        }
    }

    private IQueryable<Entities.TimelineTask> Due(DateTimeOffset now)
    {
        return db.TimelineTasks
            .AsNoTracking()
            .Where(t => t.ReminderDueAt <= now
                && t.ReminderSentAt == null
                && (t.Kind == TimelineEntryKind.CalendarEvent || t.Status != TimelineTaskStatus.Done));
    }

    /// <summary>
    /// Unsent, still due at the time read (a move in between re-armed it) and, for a Task, still open.
    /// </summary>
    private async Task<bool> ClaimAsync(DueReminder reminder, DateTimeOffset now, CancellationToken ct)
    {
        return await Due(now)
            .Where(t => t.Id == reminder.Id && t.ReminderDueAt == reminder.DueAt)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.ReminderSentAt, now), ct) == 1;
    }

    private static NotificationMessage ToMessage(DueReminder reminder, DateOnly today)
    {
        var link = reminder.Date is { } day
            ? string.Create(CultureInfo.InvariantCulture, $"/timeline?year={reminder.Year}&month={reminder.Month}&day={TaskRules.Format(day)}")
            : string.Create(CultureInfo.InvariantCulture, $"/timeline?year={reminder.Year}&month={reminder.Month}");

        return new NotificationMessage(
            NotificationCategories.TimelineReminder,
            NotificationSeverity.Info,
            reminder.Title,
            When(reminder, today),
            link,
            $"{NotificationCategories.TimelineReminder}:{reminder.Id}");
    }

    private static string When(DueReminder reminder, DateOnly today)
    {
        if (reminder.Date is not { } day)
        {
            return string.Create(CultureInfo.InvariantCulture, $"In {new DateOnly(reminder.Year, reminder.Month, 1):MMMM yyyy}");
        }

        var dayText = day == today ? "Today"
            : day == today.AddDays(1) ? "Tomorrow"
            : day.ToString("ddd d MMM yyyy", CultureInfo.InvariantCulture);

        return reminder.StartTime is { } time
            ? string.Create(CultureInfo.InvariantCulture, $"{dayText} at {time:HH:mm}")
            : dayText;
    }

    private sealed record DueReminder(
        Guid Id,
        Guid UserId,
        string Title,
        int Year,
        int Month,
        DateOnly? Date,
        TimeOnly? StartTime,
        DateOnly? EndDate,
        DateTimeOffset DueAt);
}
