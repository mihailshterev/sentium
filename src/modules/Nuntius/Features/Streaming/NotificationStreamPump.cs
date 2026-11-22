using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Sentium.Nuntius.Features.Streaming;

/// <summary>
/// One user's feed as Server-Sent Event items for a Device stream.
/// </summary>
internal static class NotificationStreamPump
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Subscribes before the connected frame, so nothing published after the client sees "connected" is lost.
    /// </summary>
    public static async IAsyncEnumerable<SseItem<string>> ReadAsync(INotificationFeed feed, Guid userId, [EnumeratorCancellation] CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(feed);

        using var subscription = feed.Subscribe(userId);

        yield return Item(new NotificationFeedEvent(NotificationFeedEventTypes.Connected));

        await foreach (var evt in subscription.Reader.ReadAllAsync(ct))
        {
            yield return Item(evt);
        }
    }

    private static SseItem<string> Item(NotificationFeedEvent evt) => new(JsonSerializer.Serialize(evt, JsonOptions));
}
