using FluentValidation;
using Sentium.Tempus.Features;

namespace Sentium.Tempus.Common;

internal static class TempusRules
{
    public static readonly string MonthRangeMessage = $"The month must be between {TempusLimits.MinYear}-01 and {TempusLimits.MaxYear}-12.";

    public static bool DayInMonth(int year, int month, DateOnly? date)
        => date is not { } day || TimelineCalendar.IsInMonth(day, year, month);
}
