using Sentium.Nuntius.Contracts;
using Sentium.Nuntius.Features.Inbox;

namespace Sentium.Nuntius.Features.Publishing;

internal interface INotificationStore
{
    /// <summary>
    /// Writes <paramref name="message"/> to the user's Inbox, updating their unread Notification with the same dedupe
    /// key instead of adding a second one. Returns the Notification as stored.
    /// </summary>
    Task<NotificationDto> SaveAsync(Guid userId, NotificationMessage message, CancellationToken ct);
}
