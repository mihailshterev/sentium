using System.Threading.Channels;

namespace Sentium.Vigil.Features.Monitoring;

public static class VigilFeedEventTypes
{
    public const string Status = "status";
    public const string IncidentOpened = "incident.opened";
    public const string IncidentResolved = "incident.resolved";
}

/// <summary>
/// One live update for the portal: <paramref name="Type"/> is a <see cref="VigilFeedEventTypes"/> value,
/// <paramref name="Data"/> a <c>ServiceHealthPayload</c> or <see cref="Incident"/>.
/// </summary>
public sealed record VigilFeedEvent(string Type, object Data);

/// <summary>
/// Broadcasts health and incident changes from <see cref="MonitoringWorker"/> to every connected SSE client.
/// </summary>
public interface IVigilFeed
{
    void Publish(VigilFeedEvent evt);

    /// <summary>
    /// Starts receiving updates published from now on; dispose the subscription to stop.
    /// </summary>
    VigilFeedSubscription Subscribe();
}

public sealed class VigilFeedSubscription(ChannelReader<VigilFeedEvent> reader, Action unsubscribe) : IDisposable
{
    public ChannelReader<VigilFeedEvent> Reader { get; } = reader;

    public void Dispose() => unsubscribe();
}

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
