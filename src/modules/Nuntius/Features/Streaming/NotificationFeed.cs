using System.Threading.Channels;

namespace Sentium.Nuntius.Features.Streaming;

/// <summary>
/// In-memory fan-out keyed by user, with a small bounded queue per open session; a slow client drops its oldest
/// events rather than slowing the publisher.
/// </summary>
internal sealed class NotificationFeed : INotificationFeed
{
    private const int SubscriberCapacity = 64;

    private readonly Lock _gate = new();
    private readonly Dictionary<Guid, List<Channel<NotificationFeedEvent>>> _subscribers = [];

    public void Publish(Guid userId, NotificationFeedEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);

        lock (_gate)
        {
            if (!_subscribers.TryGetValue(userId, out var sessions))
            {
                return;
            }

            foreach (var session in sessions)
            {
                session.Writer.TryWrite(evt);
            }
        }
    }

    public NotificationFeedSubscription Subscribe(Guid userId)
    {
        var channel = Channel.CreateBounded<NotificationFeedEvent>(new BoundedChannelOptions(SubscriberCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

        lock (_gate)
        {
            if (!_subscribers.TryGetValue(userId, out var sessions))
            {
                sessions = [];
                _subscribers[userId] = sessions;
            }

            sessions.Add(channel);
        }

        return new NotificationFeedSubscription(channel.Reader, () => Unsubscribe(userId, channel));
    }

    private void Unsubscribe(Guid userId, Channel<NotificationFeedEvent> channel)
    {
        lock (_gate)
        {
            if (_subscribers.TryGetValue(userId, out var sessions) && sessions.Remove(channel) && sessions.Count == 0)
            {
                _subscribers.Remove(userId);
            }
        }

        channel.Writer.TryComplete();
    }
}
