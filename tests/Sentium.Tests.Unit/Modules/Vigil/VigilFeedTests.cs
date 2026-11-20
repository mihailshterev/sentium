using Sentium.Vigil.Features.Monitoring;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Vigil;

public sealed class VigilFeedTests
{
    private readonly VigilFeed _sut = new();

    [Fact]
    public void Publish_ReachesEverySubscriber()
    {
        using var first = _sut.Subscribe();
        using var second = _sut.Subscribe();
        var evt = new VigilFeedEvent(VigilFeedEventTypes.Status, "payload");

        _sut.Publish(evt);

        Assert.True(first.Reader.TryRead(out var a));
        Assert.True(second.Reader.TryRead(out var b));
        Assert.Equal(evt, a);
        Assert.Equal(evt, b);
    }

    [Fact]
    public void DisposedSubscription_StopsReceiving()
    {
        var subscription = _sut.Subscribe();
        subscription.Dispose();

        _sut.Publish(new VigilFeedEvent(VigilFeedEventTypes.IncidentOpened, "payload"));

        Assert.False(subscription.Reader.TryRead(out _));
    }

    [Fact]
    public void SlowSubscriber_KeepsOnlyTheNewestUpdates()
    {
        using var subscription = _sut.Subscribe();

        for (var i = 0; i < 300; i++)
        {
            _sut.Publish(new VigilFeedEvent(VigilFeedEventTypes.Status, i));
        }

        Assert.Equal(256, subscription.Reader.Count);
        Assert.True(subscription.Reader.TryRead(out var oldest));
        Assert.Equal(44, oldest!.Data);
    }
}
