using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Sentium.Infrastructure.Realtime;

/// <summary>
/// In-memory fan-out behind <see cref="IRealtimePublisher"/>. Each event is serialized once, then offered to every
/// open session its audience includes.
/// </summary>
internal sealed class RealtimeHub(ILogger<RealtimeHub> logger) : IRealtimePublisher
{
    private const int SubscriberCapacity = 256;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly Lock _gate = new();
    private readonly List<RealtimeSubscription> _subscriptions = [];

    public static string Serialize(RealtimeFrame frame) => JsonSerializer.Serialize(frame, JsonOptions);

    public void Publish(RealtimeEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);

        string frame;
        try
        {
            frame = Serialize(new RealtimeFrame(evt.Topic, evt.Data));
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            logger.LogWarning(ex, "Could not serialize the realtime event {Topic}; it was not sent.", evt.Topic);
            return;
        }

        lock (_gate)
        {
            foreach (var subscription in _subscriptions)
            {
                if (evt.Audience.Includes(subscription.UserId, subscription.IsSovereign))
                {
                    subscription.TryWrite(frame);
                }
            }
        }
    }

    /// <summary>
    /// Starts receiving the events published from now on for <paramref name="userId"/>.
    /// </summary>
    public RealtimeSubscription Subscribe(Guid userId, bool isSovereign)
    {
        var subscription = new RealtimeSubscription(userId, isSovereign, SubscriberCapacity, Unsubscribe);

        lock (_gate)
        {
            _subscriptions.Add(subscription);
        }

        return subscription;
    }

    private void Unsubscribe(RealtimeSubscription subscription)
    {
        lock (_gate)
        {
            _subscriptions.Remove(subscription);
        }
    }
}
