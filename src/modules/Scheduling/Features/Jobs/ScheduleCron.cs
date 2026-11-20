using System.Globalization;
using Quartz;

namespace Sentium.Scheduling.Features.Jobs;

/// <summary>
/// Cron rules every Schedule follows: 6- or 7-field Quartz syntax that fires in the future, at least
/// <see cref="SchedulerDefaults.MinimumInterval"/> apart.
/// </summary>
internal static class ScheduleCron
{
    private const int IntervalSamples = 50;

    /// <summary>
    /// Why <paramref name="expression"/> cannot be used, or <see langword="null"/> when it can.
    /// </summary>
    public static string? Validate(string? expression, TimeZoneInfo zone, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(expression) || !CronExpression.IsValidExpression(expression))
        {
            return $"'{expression}' is not a valid Quartz cron expression (6 fields, e.g. '0 30 7 * * ?').";
        }

        var cron = new CronExpression(expression) { TimeZone = zone };
        if (cron.GetNextValidTimeAfter(now) is not { } firstFire)
        {
            return $"'{expression}' never fires in the future.";
        }

        if (GetShortestInterval(cron, firstFire) is { } shortest && shortest < SchedulerDefaults.MinimumInterval)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"'{expression}' fires every {shortest.TotalSeconds:0} second(s); runs must be at least {SchedulerDefaults.MinimumInterval.TotalMinutes:0} minute(s) apart.");
        }

        return null;
    }

    public static TimeSpan? GetShortestInterval(CronExpression cron, DateTimeOffset firstFire)
    {
        ArgumentNullException.ThrowIfNull(cron);

        TimeSpan? shortest = null;
        var previous = firstFire;

        for (var i = 0; i < IntervalSamples; i++)
        {
            if (cron.GetNextValidTimeAfter(previous) is not { } next)
            {
                break;
            }

            var gap = next - previous;
            if (shortest is null || gap < shortest)
            {
                shortest = gap;
            }

            previous = next;
        }

        return shortest;
    }
}
