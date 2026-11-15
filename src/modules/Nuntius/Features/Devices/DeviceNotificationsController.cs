using Microsoft.AspNetCore.Mvc;
using Sentium.Infrastructure.Security;
using Sentium.Nuntius.Features.Inbox;
using Sentium.Nuntius.Features.Streaming;

namespace Sentium.Nuntius.Features.Devices;

/// <summary>
/// What a paired Device may read of its owner's Inbox: the live stream and the unread count, nothing else. A toast
/// is a best-effort copy; a Device that was offline does not get missed Notifications replayed.
/// </summary>
/// <remarks>
/// This is deliberately not an <c>INotificationChannel</c>: the publisher already pushes every Notification to the
/// feed, and a Device is one more live subscriber. When Channel preferences and quiet hours arrive, apply them here.
/// </remarks>
[ApiController]
[AuthorizeDevice]
[Route($"{NuntiusRoutes.Prefix}/devices")]
internal sealed class DeviceNotificationsController(
    INotificationFeed feed,
    IInboxService inbox,
    DeviceStreamRegistry streams,
    ICurrentUser currentUser,
    ILogger<DeviceNotificationsController> logger) : ControllerBase
{
    [HttpGet("stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task Stream(CancellationToken ct)
    {
        if (User.GetDeviceId() is not { } deviceId || currentUser.UserId is not { } ownerUserId)
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        using var lease = streams.Open(deviceId, ct);

        try
        {
            await NotificationStreamPump.RunAsync(Response, feed, ownerUserId, lease.Token);
        }
        catch (OperationCanceledException)
        {
            logger.LogDebug("Device {DeviceId} left the notification stream", deviceId);
        }
    }

    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(UnreadCountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UnreadCount(CancellationToken ct)
        => Ok(new UnreadCountDto(await inbox.CountUnreadAsync(ct)));
}
