using FluentAssertions;
using Sentium.Vigil.Features.Monitoring;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Vigil;

public sealed class WatchdogFeedTests
{
    private readonly WatchdogFeed _sut = new();

    [Fact]
    public void Publish_ReachesEverySubscriber()
    {
        using var first = _sut.Subscribe();
        using var second = _sut.Subscribe();
        var evt = new WatchdogFeedEvent(WatchdogFeedEventTypes.Status, "payload");

        _sut.Publish(evt);

        first.Reader.TryRead(out var a).Should().BeTrue();
        second.Reader.TryRead(out var b).Should().BeTrue();
        a.Should().Be(evt);
        b.Should().Be(evt);
    }

    [Fact]
    public void DisposedSubscription_StopsReceiving()
    {
        var subscription = _sut.Subscribe();
        subscription.Dispose();

        _sut.Publish(new WatchdogFeedEvent(WatchdogFeedEventTypes.IncidentOpened, "payload"));

        subscription.Reader.TryRead(out _).Should().BeFalse();
    }

    [Fact]
    public void SlowSubscriber_KeepsOnlyTheNewestUpdates()
    {
        using var subscription = _sut.Subscribe();

        for (var i = 0; i < 300; i++)
        {
            _sut.Publish(new WatchdogFeedEvent(WatchdogFeedEventTypes.Status, i));
        }

        subscription.Reader.Count.Should().Be(256);
        subscription.Reader.TryRead(out var oldest).Should().BeTrue();
        oldest!.Data.Should().Be(44);
    }
}
