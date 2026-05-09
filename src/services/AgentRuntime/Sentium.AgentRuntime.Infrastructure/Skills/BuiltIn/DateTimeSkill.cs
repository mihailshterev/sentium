using System.ComponentModel;
using System.Text.Json;
using Microsoft.Agents.AI;
using Sentium.AgentRuntime.Core.Skills;

namespace Sentium.AgentRuntime.Infrastructure.Skills.BuiltIn;

/// <summary>
/// Built-in skill that provides date and time utilities.
/// </summary>
internal sealed class DateTimeSkill : AgentClassSkill<DateTimeSkill>
{
    internal static BuiltInSkillInfo Descriptor { get; } = new(
        "datetime-utils",
        "Date and time utilities: current date/time in any timezone, date arithmetic, duration formatting, and ISO 8601 parsing. Use for questions involving dates, times, countdowns, or scheduling.",
        """
        Use this skill when the user asks about dates, times, time zones, or durations.

        1. For the current date/time use the current-datetime script, optionally passing a timezone offset.
        2. For date arithmetic (add/subtract days, weeks, months) use the date-add script.
        3. For computing the difference between two dates use the date-diff script.
        4. Always present dates in a human-friendly format alongside the ISO 8601 form.
        5. When the user mentions "now" or "today", call current-datetime first.
        """);

    public override AgentSkillFrontmatter Frontmatter { get; } = new(
        "datetime-utils",
        "Date and time utilities: current date/time in any timezone, date arithmetic, duration formatting, and ISO 8601 parsing. Use for questions involving dates, times, countdowns, or scheduling.");

    protected override string Instructions => """
        Use this skill when the user asks about dates, times, time zones, or durations.

        1. For the current date/time use the current-datetime script, optionally passing a timezone offset.
        2. For date arithmetic (add/subtract days, weeks, months) use the date-add script.
        3. For computing the difference between two dates use the date-diff script.
        4. Always present dates in a human-friendly format alongside the ISO 8601 form.
        5. When the user mentions "now" or "today", call current-datetime first.
        """;

    [AgentSkillResource("timezone-reference")]
    [Description("Common timezone UTC offsets for reference.")]
    public string TimezoneReference => """
        # Common Timezone UTC Offsets
        | Timezone              | UTC Offset    |
        |-----------------------|---------------|
        | UTC                   | +0            |
        | London (GMT/BST)      | +0 / +1       |
        | Central European      | +1 / +2       |
        | Eastern European      | +2 / +3       |
        | Moscow                | +3            |
        | Gulf Standard Time    | +4            |
        | India Standard Time   | +5:30         |
        | China Standard Time   | +8            |
        | Japan Standard Time   | +9            |
        | Eastern US (EST/EDT)  | -5 / -4       |
        | Central US (CST/CDT)  | -6 / -5       |
        | Mountain US (MST/MDT) | -7 / -6       |
        | Pacific US (PST/PDT)  | -8 / -7       |
        """;

    [AgentSkillScript("current-datetime")]
    [Description("Returns the current UTC date and time, optionally shifted by a UTC offset in hours (e.g. 1 for UTC+1).")]
    private static string CurrentDateTime(double utcOffsetHours = 0)
    {
        var now = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(utcOffsetHours));
        return JsonSerializer.Serialize(new
        {
            iso = now.ToString("O"),
            readable = now.ToString("dddd, dd MMMM yyyy HH:mm:ss zzz"),
            date = now.ToString("yyyy-MM-dd"),
            time = now.ToString("HH:mm:ss"),
            utcOffset = utcOffsetHours
        });
    }

    [AgentSkillScript("date-add")]
    [Description("Adds days, weeks, or months to a base date (ISO 8601 format) and returns the resulting date.")]
    private static string DateAdd(string baseDate, int days = 0, int weeks = 0, int months = 0)
    {
        if (!DateTimeOffset.TryParse(baseDate, out var date))
        {
            return JsonSerializer.Serialize(new { error = "Invalid date format. Use ISO 8601 (e.g. 2026-05-09)." });
        }

        var result = date
            .AddDays(days)
            .AddDays(weeks * 7)
            .AddMonths(months);

        return JsonSerializer.Serialize(new
        {
            input = baseDate,
            added = new { days, weeks, months },
            result = result.ToString("yyyy-MM-dd"),
            readable = result.ToString("dddd, dd MMMM yyyy")
        });
    }

    [AgentSkillScript("date-diff")]
    [Description("Computes the difference between two ISO 8601 dates and returns days, weeks, and months.")]
    private static string DateDiff(string fromDate, string toDate)
    {
        if (!DateTimeOffset.TryParse(fromDate, out var from) || !DateTimeOffset.TryParse(toDate, out var to))
        {
            return JsonSerializer.Serialize(new { error = "Invalid date format. Use ISO 8601." });
        }

        var span = to - from;
        var totalDays = (int)Math.Abs(span.TotalDays);
        var direction = span.TotalDays >= 0 ? "future" : "past";

        return JsonSerializer.Serialize(new
        {
            from = fromDate,
            to = toDate,
            direction,
            totalDays,
            weeks = totalDays / 7,
            remainingDays = totalDays % 7
        });
    }
}
