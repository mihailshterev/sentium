using FluentAssertions;
using Sentium.Tempus.Common;
using Sentium.Tempus.Contracts.Models;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Tempus;

public sealed class TimelineCalendarTests
{
    private static readonly DateOnly Today = new(2026, 11, 7);

    [Fact]
    public void EffectiveDeadline_PrefersTheDueDate_ThenThePlannedDay_ThenTheMonthEnd()
    {
        TimelineCalendar.EffectiveDeadline(new DateOnly(2026, 11, 20), new DateOnly(2026, 11, 3), 2026, 11).Should().Be(new DateOnly(2026, 11, 20));
        TimelineCalendar.EffectiveDeadline(null, new DateOnly(2026, 11, 3), 2026, 11).Should().Be(new DateOnly(2026, 11, 3));
        TimelineCalendar.EffectiveDeadline(null, null, 2026, 2).Should().Be(new DateOnly(2026, 2, 28));
        TimelineCalendar.EffectiveDeadline(null, null, 2028, 2).Should().Be(new DateOnly(2028, 2, 29));
    }

    [Theory]
    [InlineData(TimelineTaskStatus.Todo, 6, true)]
    [InlineData(TimelineTaskStatus.InProgress, 6, true)]
    [InlineData(TimelineTaskStatus.Done, 6, false)]
    [InlineData(TimelineTaskStatus.Todo, 7, false)]
    public void IsOverdue_OnlyForOpenTasksPastTheirDay(TimelineTaskStatus status, int day, bool expected)
        => TimelineCalendar.IsOverdue(status, null, new DateOnly(2026, 11, day), 2026, 11, Today).Should().Be(expected);

    [Fact]
    public void IsOverdue_ForAPinnedTask_WaitsForTheMonthEnd()
    {
        TimelineCalendar.IsOverdue(TimelineTaskStatus.Todo, null, null, 2026, 11, Today).Should().BeFalse();
        TimelineCalendar.IsOverdue(TimelineTaskStatus.Todo, null, null, 2026, 10, Today).Should().BeTrue();
    }

    [Theory]
    [InlineData(6, 6)]
    [InlineData(8, 8)]
    [InlineData(9, 7)]
    [InlineData(1, 7)]
    public void ResolveToday_TrustsTheClient_OnlyWithinADayOfUtc(int clientDay, int expectedDay)
        => TimelineCalendar.ResolveToday(new DateOnly(2026, 11, clientDay), Today).Should().Be(new DateOnly(2026, 11, expectedDay));

    [Fact]
    public void ResolveToday_WithoutAClientDate_IsUtc()
        => TimelineCalendar.ResolveToday(null, Today).Should().Be(Today);

    [Theory]
    [InlineData(6, null, true)]
    [InlineData(7, null, false)]
    [InlineData(5, 6, true)]
    [InlineData(5, 7, false)]
    public void IsPast_ForADayEvent_AfterItsLastDay(int day, int? endDay, bool expected)
    {
        var end = endDay is { } e ? new DateOnly(2026, 11, e) : (DateOnly?)null;

        TimelineCalendar.IsPast(new DateOnly(2026, 11, day), end, 2026, 11, Today).Should().Be(expected);
    }

    [Fact]
    public void IsPast_ForAPinnedEvent_OnceItsMonthIsOver()
    {
        TimelineCalendar.IsPast(null, null, 2026, 10, Today).Should().BeTrue();
        TimelineCalendar.IsPast(null, null, 2026, 11, Today).Should().BeFalse();
        TimelineCalendar.IsPast(null, null, 2026, 11, new DateOnly(2026, 12, 1)).Should().BeTrue();
    }

    [Fact]
    public void TodayIn_UsesTheZonesDate_NotUtcs()
    {
        var sofia = TimeZoneInfo.FindSystemTimeZoneById("Europe/Sofia");
        var lateUtc = new DateTimeOffset(2026, 11, 6, 22, 30, 0, TimeSpan.Zero);

        TimelineCalendar.TodayIn(sofia, lateUtc).Should().Be(Today);
        TimelineCalendar.TodayIn(TimeZoneInfo.Utc, lateUtc).Should().Be(new DateOnly(2026, 11, 6));
    }

    [Fact]
    public void AnEventOnYesterday_InSofia_IsPastJustAfterLocalMidnight()
    {
        var sofia = TimeZoneInfo.FindSystemTimeZoneById("Europe/Sofia");
        var today = TimelineCalendar.TodayIn(sofia, new DateTimeOffset(2026, 10, 31, 22, 5, 0, TimeSpan.Zero));

        TimelineCalendar.IsPast(new DateOnly(2026, 10, 31), null, 2026, 10, today).Should().BeTrue();
        TimelineCalendar.IsPast(null, null, 2026, 10, today).Should().BeTrue();
    }

    [Fact]
    public void MonthIndex_OrdersMonthsAcrossYears()
    {
        TimelineCalendar.MonthIndex(2026, 12).Should().BeLessThan(TimelineCalendar.MonthIndex(2027, 1));
        (TimelineCalendar.MonthIndex(2027, 1) - TimelineCalendar.MonthIndex(2026, 12)).Should().Be(1);
    }
}
