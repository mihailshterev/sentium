using FluentAssertions;
using Sentium.Backup.Running;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Backup;

public sealed class BackupScheduleTests
{
    private static readonly TimeOnly ThreeAm = new(3, 0);

    [Fact]
    public void BeforeTheTime_RunsLaterToday()
    {
        var now = new DateTimeOffset(2026, 11, 15, 1, 0, 0, TimeSpan.Zero);

        BackupSchedule.NextOccurrence(now, ThreeAm, TimeZoneInfo.Utc)
            .Should().Be(new DateTimeOffset(2026, 11, 15, 3, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void AtOrAfterTheTime_RunsTomorrow()
    {
        var now = new DateTimeOffset(2026, 11, 15, 3, 0, 0, TimeSpan.Zero);

        BackupSchedule.NextOccurrence(now, ThreeAm, TimeZoneInfo.Utc)
            .Should().Be(new DateTimeOffset(2026, 11, 16, 3, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void TimeOfDay_IsReadInTheGivenZone()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("plus2", TimeSpan.FromHours(2), "plus2", "plus2");
        var now = new DateTimeOffset(2026, 11, 15, 0, 30, 0, TimeSpan.Zero);

        var next = BackupSchedule.NextOccurrence(now, ThreeAm, zone);

        next.Should().Be(new DateTimeOffset(2026, 11, 15, 3, 0, 0, TimeSpan.FromHours(2)));
        next.UtcDateTime.Hour.Should().Be(1);
    }

    [Fact]
    public void TimeSkippedByDaylightSaving_MovesForwardByAnHour()
    {
        var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            DateTime.MinValue.Date,
            DateTime.MaxValue.Date,
            TimeSpan.FromHours(1),
            TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 2, 0, 0), 3, 29),
            TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 3, 0, 0), 10, 25));
        var zone = TimeZoneInfo.CreateCustomTimeZone("dst", TimeSpan.Zero, "dst", "std", "dst", [rule]);
        var now = new DateTimeOffset(2026, 3, 28, 12, 0, 0, TimeSpan.Zero);

        var next = BackupSchedule.NextOccurrence(now, new TimeOnly(2, 30), zone);

        next.Should().Be(new DateTimeOffset(2026, 3, 29, 3, 30, 0, TimeSpan.FromHours(1)));
    }
}
