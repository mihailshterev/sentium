using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentium.Core.Constants;
using Sentium.Infrastructure.Security;

namespace Sentium.Nuntius.Features.Streaming;

/// <summary>
/// Server-Sent Events stream of the signed-in user's Notifications as they are published or read. It carries only
/// what happens while connected; clients fetch the unread count by REST on (re)connect.
/// </summary>
[ApiController]
[Authorize]
[Route($"{NuntiusRoutes.Prefix}/stream")]
internal sealed class NotificationStreamController(
    INotificationFeed feed,
    ICurrentUser currentUser,
    ILogger<NotificationStreamController> logger) : ControllerBase
{
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(20);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task Stream(CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId)
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        Response.Headers.Append(CommonHeaderNames.ContentType, "text/event-stream");
        Response.Headers.Append(CommonHeaderNames.CacheControl, "no-cache");
        Response.Headers.Append(CommonHeaderNames.Connection, "keep-alive");
        Response.Headers.Append(CommonHeaderNames.AccelBuffering, "no");

        // Subscribe before the connected frame, so nothing published after the client sees "connected" is lost.
        using var subscription = feed.Subscribe(userId);

        try
        {
            await WriteFrameAsync(new NotificationFeedEvent(NotificationFeedEventTypes.Connected), ct);
            await PumpAsync(subscription, ct);
        }
        catch (OperationCanceledException)
        {
            logger.LogDebug("Client disconnected from the notification stream");
        }
    }

    private async Task PumpAsync(NotificationFeedSubscription subscription, CancellationToken ct)
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
                await Response.WriteAsync(": heartbeat\n\n", ct);
                await Response.Body.FlushAsync(ct);
                continue;
            }

            while (subscription.Reader.TryRead(out var evt))
            {
                await WriteFrameAsync(evt, ct);
            }
        }
    }

    private async Task WriteFrameAsync(NotificationFeedEvent evt, CancellationToken ct)
    {
        await Response.WriteAsync($"data: {JsonSerializer.Serialize(evt, JsonOptions)}\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }
}
