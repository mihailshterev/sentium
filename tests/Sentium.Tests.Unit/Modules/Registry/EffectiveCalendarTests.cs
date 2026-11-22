using System.Text.Json;
using NSubstitute;
using Sentium.Core.Constants;
using Sentium.Registry.Contracts;
using Sentium.Registry.Contracts.Settings;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Registry;

public sealed class EffectiveCalendarTests
{
    private readonly ISettingsService _settings = Substitute.For<ISettingsService>();
    private readonly Guid _userId = Guid.NewGuid();

    [Fact]
    public async Task UsersOwnSettings_WinAndSkipTheHouseholdRead()
    {
        var ct = TestContext.Current.CancellationToken;
        Returns(SettingsKeys.UserCalendar, _userId, new { timeZone = "Asia/Tokyo", dayStart = "06:30:00" });
        Returns(SettingsKeys.Calendar, null, new { timeZone = "Europe/Sofia", dayStart = "09:00:00" });

        var calendar = await _settings.GetEffectiveCalendarAsync(_userId, ct);

        Assert.Equal("Asia/Tokyo", calendar.TimeZone.Id);
        Assert.Equal(new TimeOnly(6, 30), calendar.DayStart);
        await _settings.DidNotReceive().GetAsync(SettingsKeys.Calendar, Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EachMissingField_FallsBackToTheHousehold()
    {
        var ct = TestContext.Current.CancellationToken;
        Returns(SettingsKeys.UserCalendar, _userId, new { timeZone = "Asia/Tokyo" });
        Returns(SettingsKeys.Calendar, null, new { timeZone = "Europe/Sofia", dayStart = "09:00:00" });

        var calendar = await _settings.GetEffectiveCalendarAsync(_userId, ct);

        Assert.Equal("Asia/Tokyo", calendar.TimeZone.Id);
        Assert.Equal(new TimeOnly(9, 0), calendar.DayStart);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WithoutAUser_OnlyTheHouseholdApplies(bool emptyGuid)
    {
        var ct = TestContext.Current.CancellationToken;
        Returns(SettingsKeys.Calendar, null, new { timeZone = "Europe/Sofia", dayStart = "09:00:00" });

        var calendar = await _settings.GetEffectiveCalendarAsync(emptyGuid ? Guid.Empty : null, ct);

        Assert.Equal("Europe/Sofia", calendar.TimeZone.Id);
        await _settings.DidNotReceive().GetAsync(SettingsKeys.UserCalendar, Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnknownZone_ResolvesToUtc()
    {
        var ct = TestContext.Current.CancellationToken;
        Returns(SettingsKeys.UserCalendar, _userId, new { timeZone = "Mars/Olympus_Mons" });
        Returns(SettingsKeys.Calendar, null, new { timeZone = "Europe/Sofia" });

        var calendar = await _settings.GetEffectiveCalendarAsync(_userId, ct);

        Assert.Equal(TimeZoneInfo.Utc, calendar.TimeZone);
    }

    [Fact]
    public async Task NothingSet_IsUtcWithTheDefaultDayStart()
    {
        var calendar = await _settings.GetEffectiveCalendarAsync(_userId, TestContext.Current.CancellationToken);

        Assert.Equal(TimeZoneInfo.Utc, calendar.TimeZone);
        Assert.Equal(CalendarSettings.DefaultDayStart, calendar.DayStart);
    }

    private void Returns(string key, Guid? userId, object value)
    {
        _settings.GetAsync(key, userId, Arg.Any<CancellationToken>())
            .Returns(new SettingsEnvelope(key, JsonSerializer.SerializeToElement(value), DateTimeOffset.UnixEpoch, null));
    }
}
