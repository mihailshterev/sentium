using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentium.Nuntius.Common;

namespace Sentium.Nuntius.Features.Inbox;

/// <summary>
/// The signed-in user's Inbox. There is no view of anyone else's, not even for a Sovereign.
/// </summary>
[ApiController]
[Authorize]
[Route($"{NuntiusRoutes.Prefix}/notifications")]
internal sealed class NotificationsController(IInboxService inbox) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<NotificationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] bool unreadOnly = false,
        [FromQuery] int limit = NuntiusLimits.DefaultPageSize,
        CancellationToken ct = default)
    {
        return Ok(await inbox.ListAsync(unreadOnly, limit, ct));
    }

    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(UnreadCountDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UnreadCount(CancellationToken ct)
        => Ok(new UnreadCountDto(await inbox.CountUnreadAsync(ct)));

    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
        => await inbox.MarkReadAsync(id, ct) ? NoContent() : NotFound();

    /// <summary>
    /// Marks every unread Notification read and returns how many there were.
    /// </summary>
    [HttpPost("read-all")]
    [ProducesResponseType(typeof(UnreadCountDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
        => Ok(new UnreadCountDto(await inbox.MarkAllReadAsync(ct)));
}
