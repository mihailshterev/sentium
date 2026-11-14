using Sentium.Tempus.Contracts.Models;

namespace Sentium.Tempus.Common;

/// <summary>
/// The rule of a Series: every <paramref name="Interval"/> days, weeks, months or years from its first occurrence.
/// </summary>
/// <param name="Weekdays">Weekly only; <see langword="null"/> or empty repeats on the first occurrence's weekday.</param>
/// <param name="Until">The last day an occurrence may fall on; <see langword="null"/> repeats without end.</param>
internal sealed record TimelineRepeat(
    TimelineRepeatFrequency Frequency,
    int Interval,
    IReadOnlyList<DayOfWeek>? Weekdays,
    DateOnly? Until);
