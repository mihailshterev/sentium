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

    [Fact]
    public void MonthIndex_OrdersMonthsAcrossYears()
    {
        TimelineCalendar.MonthIndex(2026, 12).Should().BeLessThan(TimelineCalendar.MonthIndex(2027, 1));
        (TimelineCalendar.MonthIndex(2027, 1) - TimelineCalendar.MonthIndex(2026, 12)).Should().Be(1);
    }
}
