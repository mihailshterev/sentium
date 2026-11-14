using System.Threading.Channels;

namespace Sentium.Vigil.Features.Monitoring;

/// <summary>
/// In-memory fan-out with a small bounded queue per subscriber; a slow client drops its oldest updates
/// rather than slowing the monitoring loop. Updates published while nobody is connected are discarded.
/// </summary>
public sealed class VigilFeed : IVigilFeed
{
    private const int SubscriberCapacity = 256;

    private readonly Lock _gate = new();
    private readonly List<Channel<VigilFeedEvent>> _subscribers = [];

    public void Publish(VigilFeedEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);

        lock (_gate)
        {
            foreach (var subscriber in _subscribers)
            {
                subscriber.Writer.TryWrite(evt);
            }
        }
    }

    public VigilFeedSubscription Subscribe()
    {
        var channel = Channel.CreateBounded<VigilFeedEvent>(new BoundedChannelOptions(SubscriberCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

        lock (_gate)
        {
            _subscribers.Add(channel);
        }

        return new VigilFeedSubscription(channel.Reader, () =>
        {
            lock (_gate)
            {
                _subscribers.Remove(channel);
            }
        });
    }
}
