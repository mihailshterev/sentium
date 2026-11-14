namespace Sentium.Praxis.Features.Tools.Timeline;

/// <summary>
/// Where on the timeline something goes: a day, or a month without a day.
/// </summary>
internal readonly record struct TimelinePlacement(int Year, int Month, DateOnly? Date)
{
    public string Describe()
    {
        return Date is { } date
            ? $"{TimelineToolText.Format(date)} ({TimelineToolText.Weekday(date)})"
            : $"{TimelineToolText.FormatMonth(Year, Month)} (pinned to the month)";
    }

    /// <summary>
    /// A day when <paramref name="date"/> is given, else the month; <see langword="null"/> with an error otherwise.
    /// </summary>
    public static TimelinePlacement? Parse(string? date, string? month, DateOnly today, out string? error)
    {
        error = null;

        if (TimelineToolText.Normalize(date) is not null)
        {
            if (!TimelineToolText.TryParseDate(date, today, out var day))
            {
                error = TimelineToolText.DateError("date", today);
                return null;
            }

            return new TimelinePlacement(day.Year, day.Month, day);
        }

        if (TimelineToolText.Normalize(month) is not null)
        {
            if (!TimelineToolText.TryParseMonth(month, today, out var year, out var monthNumber))
            {
                error = TimelineToolText.MonthError("month", today);
                return null;
            }

            return new TimelinePlacement(year, monthNumber, null);
        }

        return null;
    }
}
