using Microsoft.EntityFrameworkCore;
using Sentium.Core.Constants;
using Sentium.Infrastructure.Security;
using Sentium.Registry.Contracts;
using Sentium.Tempus.Data;
using Sentium.Tempus.Features.Calendar;
using Sentium.Tempus.Features.Tasks;

namespace Sentium.Tempus.Features.Reminders;

/// <summary>
/// Recomputes unsent Reminders when a time zone or day-start changes: the user's own after a change to their
/// <see cref="SettingsKeys.UserCalendar"/>, everyone's after a change to the household's
/// <see cref="SettingsKeys.Calendar"/>. Sent Reminders are left alone.
/// </summary>
internal sealed class TimelineReminderRearmer(IServiceScopeFactory scopeFactory) : ISettingsChangedHandler
{
    public async Task OnSettingsChangedAsync(string key, Guid? userId, CancellationToken ct = default)
    {
        var forUser = key == SettingsKeys.UserCalendar && userId is not null;
        if (!forUser && key != SettingsKeys.Calendar)
        {
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<SystemScopeContext>().Activate();
        await RearmAsync(scope.ServiceProvider, forUser ? userId : null, ct);
    }

    private static async Task RearmAsync(IServiceProvider services, Guid? userId, CancellationToken ct)
    {
        var db = services.GetRequiredService<TempusDbContext>();
        var calendars = services.GetRequiredService<IUserCalendarProvider>();

        var unsent = db.TimelineTasks.Where(t => t.ReminderOffsetMinutes != null && t.ReminderSentAt == null);
        if (userId is { } id)
        {
            unsent = unsent.Where(t => t.UserId == id);
        }

        foreach (var group in (await unsent.ToListAsync(ct)).GroupBy(t => t.UserId))
        {
            var calendar = await calendars.GetAsync(group.Key, ct);
            foreach (var task in group)
            {
                TaskRules.ArmReminder(task, calendar);
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
