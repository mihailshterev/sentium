using Sentium.Infrastructure.Realtime;

namespace Sentium.Vigil.Features.Monitoring;

internal sealed class VigilFeed(IRealtimePublisher realtime) : IVigilFeed
{
    public void Publish(VigilFeedEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);

        var audience = evt.AudienceUserId is { } userId
            ? RealtimeAudience.User(userId, includeSovereigns: true)
            : RealtimeAudience.Everyone;

        realtime.Publish(new RealtimeEvent(ToTopic(evt.Type), audience, evt.Data));
    }

    private static string ToTopic(string type) => type switch
    {
        VigilFeedEventTypes.Status => RealtimeTopics.VigilStatus,
        VigilFeedEventTypes.IncidentOpened => RealtimeTopics.VigilIncidentOpened,
        VigilFeedEventTypes.IncidentResolved => RealtimeTopics.VigilIncidentResolved,
        VigilFeedEventTypes.DeviceUpdated => RealtimeTopics.VigilDeviceUpdated,
        VigilFeedEventTypes.DeviceRemoved => RealtimeTopics.VigilDeviceRemoved,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown Vigil feed event type.")
    };
}
