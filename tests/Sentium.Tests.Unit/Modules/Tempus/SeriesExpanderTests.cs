using FluentAssertions;
using Sentium.Tempus.Common;
using Sentium.Tempus.Contracts.Models;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Tempus;

public sealed class SeriesExpanderTests
{
    private static List<DateOnly> Expand(TimelineRepeat rule, DateOnly anchor, DateOnly from, DateOnly through)
        => [.. SeriesExpander.Occurrences(rule, anchor, from, through)];

    private static DateOnly D(int year, int month, int day) => new(year, month, day);

    [Fact]
    public void Daily_StepsByTheInterval_AndStopsAtTheEndDate()
    {
        var rule = new TimelineRepeat(TimelineRepeatFrequency.Daily, 3, null, D(2026, 11, 10));

        Expand(rule, D(2026, 11, 1), D(2026, 11, 1), D(2026, 12, 31))
            .Should().Equal(D(2026, 11, 1), D(2026, 11, 4), D(2026, 11, 7), D(2026, 11, 10));
    }

    [Fact]
    public void Monthly_OnThe31st_FallsOnTheLastDayOfShortMonths_WithoutDrifting()
    {
        var rule = new TimelineRepeat(TimelineRepeatFrequency.Monthly, 1, null, null);

        Expand(rule, D(2027, 1, 31), D(2027, 1, 1), D(2027, 4, 30))
            .Should().Equal(D(2027, 1, 31), D(2027, 2, 28), D(2027, 3, 31), D(2027, 4, 30));
    }

    [Fact]
    public void Yearly_OnALeapDay_FallsOnFeb28InOtherYears()
    {
        var rule = new TimelineRepeat(TimelineRepeatFrequency.Yearly, 1, null, null);

        Expand(rule, D(2028, 2, 29), D(2028, 1, 1), D(2032, 12, 31))
            .Should().Equal(D(2028, 2, 29), D(2029, 2, 28), D(2030, 2, 28), D(2031, 2, 28), D(2032, 2, 29));
    }

    [Fact]
    public void Weekly_OnSeveralWeekdays_EveryOtherWeek()
    {
        // 2026-11-04 is a Wednesday.
        var rule = new TimelineRepeat(TimelineRepeatFrequency.Weekly, 2, [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday], null);

        Expand(rule, D(2026, 11, 4), D(2026, 11, 1), D(2026, 11, 30))
            .Should().Equal(D(2026, 11, 4), D(2026, 11, 6), D(2026, 11, 16), D(2026, 11, 18), D(2026, 11, 20), D(2026, 11, 30));
    }

    [Fact]
    public void Weekly_WithoutWeekdays_RepeatsOnTheAnchorsWeekday()
    {
        var rule = new TimelineRepeat(TimelineRepeatFrequency.Weekly, 1, null, null);

        Expand(rule, D(2026, 11, 4), D(2026, 11, 1), D(2026, 11, 30))
            .Should().Equal(D(2026, 11, 4), D(2026, 11, 11), D(2026, 11, 18), D(2026, 11, 25));
    }

    [Fact]
    public void From_SkipsEarlierOccurrences_WithoutShiftingTheRhythm()
    {
        var rule = new TimelineRepeat(TimelineRepeatFrequency.Daily, 2, null, null);

        Expand(rule, D(2026, 11, 1), D(2026, 11, 4), D(2026, 11, 8))
            .Should().Equal(D(2026, 11, 5), D(2026, 11, 7));
    }

    [Fact]
    public void NeverRunsPastTheSupportedYearRange()
    {
        var rule = new TimelineRepeat(TimelineRepeatFrequency.Yearly, 1, null, null);

        Expand(rule, D(2099, 6, 1), D(2099, 1, 1), DateOnly.MaxValue)
            .Should().Equal(D(2099, 6, 1), D(2100, 6, 1));
    }
}
