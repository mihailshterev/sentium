namespace Sentium.Infrastructure.Realtime;

/// <summary>
/// Pushes change events to the portal sessions of the event's audience over the one realtime stream
/// (<c>GET /api/realtime/stream</c>). Events are hints, not state: the portal refetches over REST when it gets one,
/// and refetches everything it shows after every (re)connect, so an event published while nobody is connected is
/// simply discarded. Never blocks and never throws; a slow client drops its oldest events.
/// </summary>
public interface IRealtimePublisher
{
    void Publish(RealtimeEvent evt);
}
