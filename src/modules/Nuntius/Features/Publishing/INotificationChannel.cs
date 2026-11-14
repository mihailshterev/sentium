using Sentium.Nuntius.Features.Inbox;

namespace Sentium.Nuntius.Features.Publishing;

/// <summary>
/// An additional way to deliver a Notification beyond the Inbox (for example a phone push). Implementations are
/// resolved from DI and run after the Inbox write; each decides from the user's preferences whether it delivers.
/// </summary>
internal interface INotificationChannel
{
    string Name { get; }

    Task DeliverAsync(Guid userId, NotificationDto notification, CancellationToken ct);
}
