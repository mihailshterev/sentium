using Sentium.Tempus.Common;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Tempus;

public sealed class ReminderScheduleTests
{
    private static readonly TimeZoneInfo Sofia = TimeZoneInfo.FindSystemTimeZoneById("Europe/Sofia");
    private static readonly TimeOnly DayStart = new(8, 0);

    private static DateTimeOffset Utc(int year, int month, int day, int hour, int minute = 0)
        => new(year, month, day, hour, minute, 0, TimeSpan.Zero);

    [Fact]
    public void ATimedEntry_FiresTheOffsetBeforeItsLocalTime()
    {
        // 14:30 in Sofia (UTC+2 in November), 90 minutes before.
        Assert.Equal(Utc(2026, 11, 20, 11, 0), ReminderSchedule.DueAt(new DateOnly(2026, 11, 20), new TimeOnly(14, 30), 2026, 11, 90, Sofia, DayStart));
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(ReminderSchedule.DayBefore, 19)]
    [InlineData(ReminderSchedule.WeekBefore, 13)]
    public void ADayEntry_FiresAtDayStart_OnTheChosenDay(int offset, int expectedDay)
    {
        Assert.Equal(Utc(2026, 11, expectedDay, 6, 0), ReminderSchedule.DueAt(new DateOnly(2026, 11, 20), null, 2026, 11, offset, Sofia, DayStart));
    }

    [Fact]
    public void APinnedEntry_FiresOnTheFirstOfItsMonth_AtDayStart()
    {
        Assert.Equal(Utc(2027, 7, 1, 5, 0), ReminderSchedule.DueAt(null, null, 2027, 7, ReminderSchedule.WeekBefore, Sofia, DayStart));
    }

    [Fact]
    public void ALocalTimeInTheSpringForwardGap_MovesPastTheGap()
    {
        // Sofia skips 03:00-04:00 on 2027-03-28; 03:30 local does not exist and fires at 04:30 (01:30 UTC).
        Assert.Equal(Utc(2027, 3, 28, 1, 30), ReminderSchedule.DueAt(new DateOnly(2027, 3, 28), new TimeOnly(3, 30), 2027, 3, 0, Sofia, DayStart));
    }
}
