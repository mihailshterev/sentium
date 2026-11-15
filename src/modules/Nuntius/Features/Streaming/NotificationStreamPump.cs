using System.Text.Json;
using Sentium.Core.Constants;

namespace Sentium.Nuntius.Features.Streaming;

/// <summary>
/// Writes one user's feed to an HTTP response as Server-Sent Events until the client goes away or the subscription
/// ends. Shared by the portal stream and the Device stream, which differ only in how the caller is authenticated.
/// </summary>
internal static class NotificationStreamPump
{
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(20);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Subscribes before the connected frame, so nothing published after the client sees "connected" is lost. Throws
    /// <see cref="OperationCanceledException"/> when <paramref name="ct"/> fires.
    /// </summary>
    public static async Task RunAsync(HttpResponse response, INotificationFeed feed, Guid userId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(feed);

        response.Headers.Append(CommonHeaderNames.ContentType, "text/event-stream");
        response.Headers.Append(CommonHeaderNames.CacheControl, "no-cache");
        response.Headers.Append(CommonHeaderNames.Connection, "keep-alive");
        response.Headers.Append(CommonHeaderNames.AccelBuffering, "no");

        using var subscription = feed.Subscribe(userId);

        await WriteFrameAsync(response, new NotificationFeedEvent(NotificationFeedEventTypes.Connected), ct);
        await PumpAsync(response, subscription, ct);
    }

    private static async Task PumpAsync(HttpResponse response, NotificationFeedSubscription subscription, CancellationToken ct)
    {
        while (true)
        {
            using var waitCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            waitCts.CancelAfter(HeartbeatInterval);

            try
            {
                if (!await subscription.Reader.WaitToReadAsync(waitCts.Token))
                {
                    return;
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                await response.WriteAsync(": heartbeat\n\n", ct);
                await response.Body.FlushAsync(ct);
                continue;
            }

            while (subscription.Reader.TryRead(out var evt))
            {
                await WriteFrameAsync(response, evt, ct);
            }
        }
    }

    private static async Task WriteFrameAsync(HttpResponse response, NotificationFeedEvent evt, CancellationToken ct)
    {
        await response.WriteAsync($"data: {JsonSerializer.Serialize(evt, JsonOptions)}\n\n", ct);
        await response.Body.FlushAsync(ct);
    }
}
