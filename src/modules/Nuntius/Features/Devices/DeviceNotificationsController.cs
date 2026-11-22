using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Mvc;
using Sentium.Infrastructure.Security;
using Sentium.Infrastructure.Streaming;
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
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IResult Stream()
    {
        if (User.GetDeviceId() is not { } deviceId || currentUser.UserId is not { } ownerUserId)
        {
            return TypedResults.Unauthorized();
        }

        var ct = HttpContext.RequestAborted;
        return SseStreams.Create(ReadAsync(deviceId, ownerUserId, ct), SseStreams.DefaultHeartbeatInterval, ct);
    }

    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(UnreadCountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UnreadCount(CancellationToken ct)
        => Ok(new UnreadCountDto(await inbox.CountUnreadAsync(ct)));

    /// <summary>
    /// The lease lives as long as the stream, so removing the Device ends it.
    /// </summary>
    private async IAsyncEnumerable<SseItem<string>> ReadAsync(Guid deviceId, Guid ownerUserId, [EnumeratorCancellation] CancellationToken ct)
    {
        using var lease = streams.Open(deviceId, ct);

        await foreach (var item in NotificationStreamPump.ReadAsync(feed, ownerUserId, lease.Token))
        {
            yield return item;
        }
    }
}
