using System.Globalization;
using Sentium.Tempus.Contracts.Models;

namespace Sentium.Praxis.Features.Tools.Timeline;

/// <summary>
/// Lenient argument parsing for the timeline tools. Small local models send blanks for omitted values, mix up case and
/// spacing in enum names, and say "today" instead of a date - all of that is accepted here; anything else gets an
/// error that repeats today's date and the expected format.
/// </summary>
internal static class TimelineToolText
{
    public const string DateFormat = "yyyy-MM-dd";
    public const string MonthFormat = "yyyy-MM";

    public static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string Format(DateOnly date) => date.ToString(DateFormat, CultureInfo.InvariantCulture);

    public static string FormatMonth(int year, int month) => string.Create(CultureInfo.InvariantCulture, $"{year:D4}-{month:D2}");

    public static string Weekday(DateOnly date) => date.DayOfWeek.ToString()[..3];

    /// <summary>
    /// <c>YYYY-MM-DD</c>, or <c>today</c>/<c>tomorrow</c>/<c>yesterday</c> relative to <paramref name="today"/>.
    /// </summary>
    public static bool TryParseDate(string? text, DateOnly today, out DateOnly date)
    {
        date = default;
        var value = Normalize(text)?.ToLowerInvariant();

        switch (value)
        {
            case null:
                return false;
            case "today":
                date = today;
                return true;
            case "tomorrow":
                date = today.AddDays(1);
                return true;
            case "yesterday":
                date = today.AddDays(-1);
                return true;
            default:
                return DateOnly.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
        }
    }

    /// <summary>
    /// <c>YYYY-MM</c>; a full date is accepted too (its month is used).
    /// </summary>
    public static bool TryParseMonth(string? text, DateOnly today, out int year, out int month)
    {
        (year, month) = (0, 0);
        var value = Normalize(text);
        if (value is null)
        {
            return false;
        }

        if (string.Equals(value, "this month", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "current", StringComparison.OrdinalIgnoreCase))
        {
            (year, month) = (today.Year, today.Month);
            return true;
        }

        if (DateTime.TryParseExact(value, MonthFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            (year, month) = (parsed.Year, parsed.Month);
            return true;
        }

        if (TryParseDate(value, today, out var date))
        {
            (year, month) = (date.Year, date.Month);
            return true;
        }

        return false;
    }

    public static TimelineTaskPriority? ParsePriority(string? text) => Compact(text) switch
    {
        "low" => TimelineTaskPriority.Low,
        "normal" or "medium" => TimelineTaskPriority.Normal,
        "high" => TimelineTaskPriority.High,
        "urgent" or "critical" => TimelineTaskPriority.Urgent,
        _ => null
    };

    public static TimelineTaskStatus? ParseStatus(string? text) => Compact(text) switch
    {
        "todo" or "open" => TimelineTaskStatus.Todo,
        "inprogress" or "started" or "doing" => TimelineTaskStatus.InProgress,
        "done" or "completed" or "complete" => TimelineTaskStatus.Done,
        _ => null
    };

    public static TimelineNoteKind? ParseNoteKind(string? text) => Compact(text) switch
    {
        null or "note" => TimelineNoteKind.Note,
        "status" or "statusupdate" or "update" => TimelineNoteKind.StatusUpdate,
        "progress" or "progressreport" or "report" => TimelineNoteKind.ProgressReport,
        _ => null
    };

    /// <summary>
    /// Comma-separated tags.
    /// </summary>
    public static IReadOnlyList<string> SplitTags(string? text)
        => Normalize(text) is { } value
            ? [.. value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)]
            : [];

    public static string DateError(string what, DateOnly today)
        => $"Error: {what} must be a date like {Format(today)} (YYYY-MM-DD). Today is {Format(today)}.";

    public static string MonthError(string what, DateOnly today)
        => $"Error: {what} must be a month like {FormatMonth(today.Year, today.Month)} (YYYY-MM).";

    /// <summary>
    /// Lower-case without spaces, dashes or underscores, so "In progress", "in_progress" and "InProgress" match.
    /// </summary>
    private static string? Compact(string? text)
        => Normalize(text) is { } value
            ? new string([.. value.Where(char.IsLetter)]).ToLowerInvariant()
            : null;
}
