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
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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
        return JsonSerializer.Serialize(new CurrentDateTimeResult(
            Iso: now.ToString("O"),
            Readable: now.ToString("dddd, dd MMMM yyyy HH:mm:ss zzz"),
            Date: now.ToString("yyyy-MM-dd"),
            Time: now.ToString("HH:mm:ss"),
            UtcOffset: utcOffsetHours), JsonOptions);
    }

    [AgentSkillScript("date-add")]
    [Description("Adds days, weeks, or months to a base date (ISO 8601 format) and returns the resulting date.")]
    private static string DateAdd(string baseDate, int days = 0, int weeks = 0, int months = 0)
    {
        if (!DateTimeOffset.TryParse(baseDate, out var date))
        {
            return JsonSerializer.Serialize(new SkillError("Invalid date format. Use ISO 8601 (e.g. 2026-05-09)."), JsonOptions);
        }

        var result = date
            .AddDays(days)
            .AddDays(weeks * 7)
            .AddMonths(months);

        return JsonSerializer.Serialize(new DateAddResult(
            Input: baseDate,
            Added: new DateComponents(days, weeks, months),
            Result: result.ToString("yyyy-MM-dd"),
            Readable: result.ToString("dddd, dd MMMM yyyy")), JsonOptions);
    }

    [AgentSkillScript("date-diff")]
    [Description("Computes the difference between two ISO 8601 dates and returns days, weeks, and months.")]
    private static string DateDiff(string fromDate, string toDate)
    {
        if (!DateTimeOffset.TryParse(fromDate, out var from) || !DateTimeOffset.TryParse(toDate, out var to))
        {
            return JsonSerializer.Serialize(new SkillError("Invalid date format. Use ISO 8601."), JsonOptions);
        }

        var span = to - from;
        var totalDays = (int)Math.Abs(span.TotalDays);
        var direction = span.TotalDays >= 0 ? "future" : "past";

        return JsonSerializer.Serialize(new DateDiffResult(
            From: fromDate,
            To: toDate,
            Direction: direction,
            TotalDays: totalDays,
            Weeks: totalDays / 7,
            RemainingDays: totalDays % 7), JsonOptions);
    }

    private sealed record CurrentDateTimeResult(string Iso, string Readable, string Date, string Time, double UtcOffset);

    private sealed record DateAddResult(string Input, DateComponents Added, string Result, string Readable);

    private sealed record DateComponents(int Days, int Weeks, int Months);

    private sealed record DateDiffResult(string From, string To, string Direction, int TotalDays, int Weeks, int RemainingDays);
}
