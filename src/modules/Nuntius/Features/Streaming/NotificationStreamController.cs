using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

        try
        {
            await NotificationStreamPump.RunAsync(Response, feed, userId, ct);
        }
        catch (OperationCanceledException)
        {
            logger.LogDebug("Client disconnected from the notification stream");
        }
    }
}
