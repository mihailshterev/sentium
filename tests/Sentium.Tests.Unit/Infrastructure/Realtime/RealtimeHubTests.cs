using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Sentium.Infrastructure.Realtime;
using Xunit;

namespace Sentium.Tests.Unit.Infrastructure.Realtime;

public sealed class RealtimeHubTests
{
    private readonly RealtimeHub _sut = new(NullLogger<RealtimeHub>.Instance);
    private readonly Guid _alice = Guid.NewGuid();
    private readonly Guid _bob = Guid.NewGuid();

    [Fact]
    public void UserEvent_ReachesOnlyThatUser()
    {
        using var alice = _sut.Subscribe(_alice, isSovereign: false);
        using var bob = _sut.Subscribe(_bob, isSovereign: false);
        using var sovereign = _sut.Subscribe(Guid.NewGuid(), isSovereign: true);

        _sut.Publish(new RealtimeEvent(RealtimeTopics.Notifications, RealtimeAudience.User(_alice)));

        Assert.True(alice.Reader.TryRead(out _));
        Assert.False(bob.Reader.TryRead(out _));
        Assert.False(sovereign.Reader.TryRead(out _));
    }

    [Fact]
    public void UserEvent_IncludingSovereigns_ReachesThemToo()
    {
        using var sovereign = _sut.Subscribe(Guid.NewGuid(), isSovereign: true);

        _sut.Publish(new RealtimeEvent(RealtimeTopics.SandboxExecutions, RealtimeAudience.User(_alice, includeSovereigns: true)));

        Assert.True(sovereign.Reader.TryRead(out _));
    }

    [Fact]
    public void SovereignEvent_SkipsMembers()
    {
        using var member = _sut.Subscribe(_alice, isSovereign: false);
        using var sovereign = _sut.Subscribe(_bob, isSovereign: true);

        _sut.Publish(new RealtimeEvent(RealtimeTopics.SentinelAudit, RealtimeAudience.Sovereigns));

        Assert.False(member.Reader.TryRead(out _));
        Assert.True(sovereign.Reader.TryRead(out _));
    }

    [Fact]
    public void EveryoneEvent_IsAFrameWithTopicAndData()
    {
        using var alice = _sut.Subscribe(_alice, isSovereign: false);

        _sut.Publish(new RealtimeEvent(RealtimeTopics.VigilStatus, RealtimeAudience.Everyone, new { Status = DayOfWeek.Monday }));

        Assert.True(alice.Reader.TryRead(out var frame));
        var json = JsonDocument.Parse(frame!).RootElement;
        Assert.Equal(RealtimeTopics.VigilStatus, json.GetProperty("topic").GetString());
        Assert.Equal("Monday", json.GetProperty("data").GetProperty("status").GetString());
    }

    [Fact]
    public void DisposedSubscription_StopsReceiving()
    {
        var subscription = _sut.Subscribe(_alice, isSovereign: false);
        subscription.Dispose();

        _sut.Publish(new RealtimeEvent(RealtimeTopics.Vault, RealtimeAudience.User(_alice)));

        Assert.False(subscription.Reader.TryRead(out _));
    }

    [Fact]
    public void SlowSubscriber_KeepsOnlyTheNewestEvents()
    {
        using var subscription = _sut.Subscribe(_alice, isSovereign: false);

        for (var i = 0; i < 300; i++)
        {
            _sut.Publish(new RealtimeEvent(RealtimeTopics.Vault, RealtimeAudience.User(_alice), i));
        }

        Assert.Equal(256, subscription.Reader.Count);
        Assert.True(subscription.Reader.TryRead(out var oldest));
        Assert.Equal(44, JsonDocument.Parse(oldest!).RootElement.GetProperty("data").GetInt32());
    }
}
