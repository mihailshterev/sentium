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
        Assert.Equal(new DateOnly(2026, 11, 20), TimelineCalendar.EffectiveDeadline(new DateOnly(2026, 11, 20), new DateOnly(2026, 11, 3), 2026, 11));
        Assert.Equal(new DateOnly(2026, 11, 3), TimelineCalendar.EffectiveDeadline(null, new DateOnly(2026, 11, 3), 2026, 11));
        Assert.Equal(new DateOnly(2026, 2, 28), TimelineCalendar.EffectiveDeadline(null, null, 2026, 2));
        Assert.Equal(new DateOnly(2028, 2, 29), TimelineCalendar.EffectiveDeadline(null, null, 2028, 2));
    }

    [Theory]
    [InlineData(TimelineTaskStatus.Todo, 6, true)]
    [InlineData(TimelineTaskStatus.InProgress, 6, true)]
    [InlineData(TimelineTaskStatus.Done, 6, false)]
    [InlineData(TimelineTaskStatus.Todo, 7, false)]
    public void IsOverdue_OnlyForOpenTasksPastTheirDay(TimelineTaskStatus status, int day, bool expected)
        => Assert.Equal(expected, TimelineCalendar.IsOverdue(status, null, new DateOnly(2026, 11, day), 2026, 11, Today));

    [Fact]
    public void IsOverdue_ForAPinnedTask_WaitsForTheMonthEnd()
    {
        Assert.False(TimelineCalendar.IsOverdue(TimelineTaskStatus.Todo, null, null, 2026, 11, Today));
        Assert.True(TimelineCalendar.IsOverdue(TimelineTaskStatus.Todo, null, null, 2026, 10, Today));
    }

    [Theory]
    [InlineData(6, 6)]
    [InlineData(8, 8)]
    [InlineData(9, 7)]
    [InlineData(1, 7)]
    public void ResolveToday_TrustsTheClient_OnlyWithinADayOfUtc(int clientDay, int expectedDay)
        => Assert.Equal(new DateOnly(2026, 11, expectedDay), TimelineCalendar.ResolveToday(new DateOnly(2026, 11, clientDay), Today));

    [Fact]
    public void ResolveToday_WithoutAClientDate_IsUtc()
        => Assert.Equal(Today, TimelineCalendar.ResolveToday(null, Today));

    [Theory]
    [InlineData(6, null, true)]
    [InlineData(7, null, false)]
    [InlineData(5, 6, true)]
    [InlineData(5, 7, false)]
    public void IsPast_ForADayEvent_AfterItsLastDay(int day, int? endDay, bool expected)
    {
        var end = endDay is { } e ? new DateOnly(2026, 11, e) : (DateOnly?)null;

        Assert.Equal(expected, TimelineCalendar.IsPast(new DateOnly(2026, 11, day), end, 2026, 11, Today));
    }

    [Fact]
    public void IsPast_ForAPinnedEvent_OnceItsMonthIsOver()
    {
        Assert.True(TimelineCalendar.IsPast(null, null, 2026, 10, Today));
        Assert.False(TimelineCalendar.IsPast(null, null, 2026, 11, Today));
        Assert.True(TimelineCalendar.IsPast(null, null, 2026, 11, new DateOnly(2026, 12, 1)));
    }

    [Fact]
    public void TodayIn_UsesTheZonesDate_NotUtcs()
    {
        var sofia = TimeZoneInfo.FindSystemTimeZoneById("Europe/Sofia");
        var lateUtc = new DateTimeOffset(2026, 11, 6, 22, 30, 0, TimeSpan.Zero);

        Assert.Equal(Today, TimelineCalendar.TodayIn(sofia, lateUtc));
        Assert.Equal(new DateOnly(2026, 11, 6), TimelineCalendar.TodayIn(TimeZoneInfo.Utc, lateUtc));
    }

    [Fact]
    public void AnEventOnYesterday_InSofia_IsPastJustAfterLocalMidnight()
    {
        var sofia = TimeZoneInfo.FindSystemTimeZoneById("Europe/Sofia");
        var today = TimelineCalendar.TodayIn(sofia, new DateTimeOffset(2026, 10, 31, 22, 5, 0, TimeSpan.Zero));

        Assert.True(TimelineCalendar.IsPast(new DateOnly(2026, 10, 31), null, 2026, 10, today));
        Assert.True(TimelineCalendar.IsPast(null, null, 2026, 10, today));
    }

    [Fact]
    public void MonthIndex_OrdersMonthsAcrossYears()
    {
        Assert.True(TimelineCalendar.MonthIndex(2026, 12) < TimelineCalendar.MonthIndex(2027, 1));
        Assert.Equal(1, (TimelineCalendar.MonthIndex(2027, 1) - TimelineCalendar.MonthIndex(2026, 12)));
    }
}
