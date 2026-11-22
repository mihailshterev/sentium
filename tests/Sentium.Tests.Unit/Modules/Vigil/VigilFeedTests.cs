using NSubstitute;
using Sentium.Infrastructure.Realtime;
using Sentium.Vigil.Features.Monitoring;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Vigil;

public sealed class VigilFeedTests
{
    private readonly IRealtimePublisher _realtime = Substitute.For<IRealtimePublisher>();
    private readonly VigilFeed _sut;
    private RealtimeEvent? _published;

    public VigilFeedTests()
    {
        _realtime.When(r => r.Publish(Arg.Any<RealtimeEvent>())).Do(call => _published = call.Arg<RealtimeEvent>());
        _sut = new VigilFeed(_realtime);
    }

    [Theory]
    [InlineData(VigilFeedEventTypes.Status, RealtimeTopics.VigilStatus)]
    [InlineData(VigilFeedEventTypes.IncidentOpened, RealtimeTopics.VigilIncidentOpened)]
    [InlineData(VigilFeedEventTypes.IncidentResolved, RealtimeTopics.VigilIncidentResolved)]
    [InlineData(VigilFeedEventTypes.DeviceUpdated, RealtimeTopics.VigilDeviceUpdated)]
    [InlineData(VigilFeedEventTypes.DeviceRemoved, RealtimeTopics.VigilDeviceRemoved)]
    public void Publish_SendsEachTypeOnItsTopic(string type, string topic)
    {
        _sut.Publish(new VigilFeedEvent(type, "payload"));

        Assert.Equal(topic, _published?.Topic);
        Assert.Equal("payload", _published?.Data);
    }

    [Fact]
    public void Publish_WithoutAudience_ReachesEveryone()
    {
        _sut.Publish(new VigilFeedEvent(VigilFeedEventTypes.Status, "payload"));

        Assert.True(_published?.Audience.IsEveryone);
    }

    [Fact]
    public void Publish_ForAUser_ReachesThatUserAndSovereigns()
    {
        var owner = Guid.NewGuid();

        _sut.Publish(new VigilFeedEvent(VigilFeedEventTypes.DeviceUpdated, "payload", owner));

        var audience = _published!.Audience;
        Assert.True(audience.Includes(owner, isSovereign: false));
        Assert.True(audience.Includes(Guid.NewGuid(), isSovereign: true));
        Assert.False(audience.Includes(Guid.NewGuid(), isSovereign: false));
    }
}
