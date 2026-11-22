using System.Text.Json;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Sentium.Core.Constants;
using Sentium.Praxis.Features.Agents;
using Sentium.Praxis.Features.Agents.Runtime;
using Sentium.Registry.Contracts;
using Sentium.Tests.Unit.Modules.Praxis.Support;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis.Agents;

public sealed class CurrentDateContextProviderTests
{
    private readonly AgentTestHost _host = new();

    [Fact]
    public void BuildBlock_GivesTheLocalDate_ZoneAndOffset()
    {
        var sofia = TimeZoneInfo.FindSystemTimeZoneById("Europe/Sofia");

        // 23:30 UTC is already the next day in Sofia.
        var block = CurrentDateContextProvider.BuildBlock(new DateTimeOffset(2026, 11, 22, 23, 30, 0, TimeSpan.Zero), sofia);

        Assert.StartsWith("### CURRENT DATE", block, StringComparison.Ordinal);
        Assert.Contains("Today is Monday, 23 November 2026 (Europe/Sofia, UTC+02:00)", block, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildBlock_WritesNegativeOffsets()
    {
        var newYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

        var block = CurrentDateContextProvider.BuildBlock(new DateTimeOffset(2026, 11, 22, 3, 0, 0, TimeSpan.Zero), newYork);

        Assert.Contains("Today is Saturday, 21 November 2026 (America/New_York, UTC-05:00)", block, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Agent_IsToldTodaysDate_InTheUsersOwnZone()
    {
        var ct = TestContext.Current.CancellationToken;
        var userId = Guid.NewGuid();
        _host.RunContext.UserId = userId;
        ReturnsZone(SettingsKeys.UserCalendar, userId, "Asia/Tokyo");
        ReturnsZone(SettingsKeys.Calendar, null, "Europe/Sofia");

        var instructions = await RunAndCaptureInstructionsAsync(ct);

        Assert.Contains("Today is Sunday, 22 November 2026 (Asia/Tokyo, UTC+09:00)", instructions, StringComparison.Ordinal);
        Assert.True(
            instructions.IndexOf("### CURRENT DATE", StringComparison.Ordinal) > instructions.IndexOf("### AGENT ROLE", StringComparison.Ordinal),
            "the date block is appended after the static prompt so the static prefix stays cacheable");
    }

    [Fact]
    public async Task Agent_FallsBackToTheHouseholdZone()
    {
        var ct = TestContext.Current.CancellationToken;
        _host.RunContext.UserId = Guid.NewGuid();
        ReturnsZone(SettingsKeys.Calendar, null, "Europe/Sofia");

        var instructions = await RunAndCaptureInstructionsAsync(ct);

        Assert.Contains("(Europe/Sofia, UTC+02:00)", instructions, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Agent_GetsTheUtcDate_WhenSettingsCannotBeRead()
    {
        var ct = TestContext.Current.CancellationToken;
        _host.RunContext.UserId = Guid.NewGuid();
        _host.CalendarSettings.GetAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("registry down"));

        var instructions = await RunAndCaptureInstructionsAsync(ct);

        Assert.Contains("Today is Sunday, 22 November 2026 (UTC, UTC+00:00)", instructions, StringComparison.Ordinal);
    }

    private void ReturnsZone(string key, Guid? userId, string zone)
    {
        var value = JsonSerializer.SerializeToElement(new { timeZone = zone });
        _host.CalendarSettings.GetAsync(key, userId, Arg.Any<CancellationToken>())
            .Returns(new SettingsEnvelope(key, value, DateTimeOffset.UnixEpoch, null));
    }

    private async Task<string> RunAndCaptureInstructionsAsync(CancellationToken ct)
    {
        var agent = await _host.CreateFactory().CreateAsync(AgentRole.GeneralAssistant, ct: ct);
        await agent.RunAsync("What did I do this year?", await agent.CreateSessionAsync(ct), cancellationToken: ct);

        return Assert.Single(_host.Model.Requests).Options!.Instructions!;
    }
}
