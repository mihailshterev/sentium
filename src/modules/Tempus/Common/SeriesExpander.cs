using Sentium.Tempus.Contracts.Models;

namespace Sentium.Tempus.Common;

/// <summary>
/// Expands a Series rule into occurrence dates. Monthly and yearly occurrences keep the first occurrence's day and
/// fall on the month's last day when it is shorter (the 31st becomes the 30th or the 28th, a leap day becomes
/// 28 February), without drifting in later months. Weeks start on Monday.
/// </summary>
internal static class SeriesExpander
{
    private static readonly DateOnly LastSupportedDay = new(TempusLimits.MaxYear, 12, 31);

    /// <summary>
    /// The occurrences from <paramref name="from"/> through <paramref name="through"/> (both inclusive), never
    /// before <paramref name="anchor"/>, after the rule's end date or beyond the supported year range.
    /// </summary>
    public static IEnumerable<DateOnly> Occurrences(TimelineRepeat rule, DateOnly anchor, DateOnly from, DateOnly through)
    {
        ArgumentNullException.ThrowIfNull(rule);

        var interval = Math.Max(1, rule.Interval);
        var start = from > anchor ? from : anchor;
        var end = Min(Min(through, rule.Until ?? DateOnly.MaxValue), LastSupportedDay);

        if (start > end)
        {
            return [];
        }

        return rule.Frequency switch
        {
            TimelineRepeatFrequency.Daily => Daily(anchor, interval, start, end),
            TimelineRepeatFrequency.Weekly => Weekly(anchor, interval, rule.Weekdays, start, end),
            TimelineRepeatFrequency.Monthly => ByMonths(anchor, interval, start, end),
            TimelineRepeatFrequency.Yearly => ByMonths(anchor, interval * 12, start, end),
            _ => []
        };
    }

    private static IEnumerable<DateOnly> Daily(DateOnly anchor, int interval, DateOnly start, DateOnly end)
    {
        var skipped = (start.DayNumber - anchor.DayNumber + interval - 1) / interval;
        for (var day = anchor.AddDays(skipped * interval); day <= end; day = day.AddDays(interval))
        {
            yield return day;
        }
    }

    private static IEnumerable<DateOnly> Weekly(DateOnly anchor, int interval, IReadOnlyList<DayOfWeek>? weekdays, DateOnly start, DateOnly end)
    {
        var offsets = weekdays is { Count: > 0 }
            ? weekdays.Select(MondayOffset).Distinct().Order().ToList()
            : [MondayOffset(anchor.DayOfWeek)];

        var firstWeek = anchor.AddDays(-MondayOffset(anchor.DayOfWeek));
        var weeksToStart = (start.DayNumber - firstWeek.DayNumber) / 7;
        var skipped = weeksToStart / interval;

        for (var week = firstWeek.AddDays(skipped * interval * 7); week <= end; week = week.AddDays(interval * 7))
        {
            foreach (var offset in offsets)
            {
                var day = week.AddDays(offset);
                if (day >= start && day <= end)
                {
                    yield return day;
                }
            }
        }
    }

    private static IEnumerable<DateOnly> ByMonths(DateOnly anchor, int monthStep, DateOnly start, DateOnly end)
    {
        var anchorIndex = TimelineCalendar.MonthIndex(anchor.Year, anchor.Month);
        var monthsToStart = TimelineCalendar.MonthIndex(start.Year, start.Month) - anchorIndex;
        var step = Math.Max(0, monthsToStart / monthStep);

        while (true)
        {
            var index = anchorIndex + (step * monthStep);
            var (year, month) = (index / 12, (index % 12) + 1);
            var day = new DateOnly(year, month, Math.Min(anchor.Day, DateTime.DaysInMonth(year, month)));

            if (day > end)
            {
                yield break;
            }

            if (day >= start)
            {
                yield return day;
            }

            step++;
        }
    }

    private static int MondayOffset(DayOfWeek day) => ((int)day + 6) % 7;

    private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;
}
