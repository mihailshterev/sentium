using Sentium.Identity.Contracts;
using Sentium.Nuntius.Contracts;
using Sentium.Nuntius.Features.Inbox;
using Sentium.Nuntius.Features.Streaming;

namespace Sentium.Nuntius.Features.Publishing;

/// <summary>
/// Resolves the recipients, then for each one independently: writes the Inbox first, pushes the Notification to the
/// user's open sessions, then hands it to every additional Channel. A failure for one recipient never stops the
/// others, and every step after the Inbox write is best-effort.
/// </summary>
internal sealed class Notifier(
    IUserDirectory userDirectory,
    INotificationStore store,
    INotificationFeed feed,
    IEnumerable<INotificationChannel> channels,
    ILogger<Notifier> logger) : INotifier
{
    private readonly IReadOnlyList<INotificationChannel> _channels = [.. channels];

    public async Task NotifyAsync(NotificationRecipient recipient, NotificationMessage message, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(recipient);
        ArgumentNullException.ThrowIfNull(message);

        IReadOnlyCollection<Guid> userIds;
        try
        {
            userIds = await ResolveRecipientsAsync(recipient, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to resolve the recipients of a {Category} notification to {Audience}", message.Category, recipient.Audience);
            return;
        }

        if (userIds.Count == 0)
        {
            logger.LogDebug("Notification {Category} for {Audience} has no recipients", message.Category, recipient.Audience);
            return;
        }

        foreach (var userId in userIds)
        {
            await PublishToUserAsync(userId, message, ct);
        }
    }

    private async Task<IReadOnlyCollection<Guid>> ResolveRecipientsAsync(NotificationRecipient recipient, CancellationToken ct)
    {
        IReadOnlyList<Guid> userIds = recipient.Audience switch
        {
            NotificationAudience.User => [recipient.UserId!.Value],
            NotificationAudience.Sovereigns => await userDirectory.GetSovereignIdsAsync(ct),
            NotificationAudience.Household => await userDirectory.GetHouseholdIdsAsync(ct),
            _ => throw new ArgumentOutOfRangeException(nameof(recipient), recipient.Audience, "Unknown audience.")
        };

        return [.. userIds.Where(id => id != Guid.Empty).Distinct()];
    }

    private async Task PublishToUserAsync(Guid userId, NotificationMessage message, CancellationToken ct)
    {
        NotificationDto notification;
        try
        {
            notification = await store.SaveAsync(userId, message, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to write a {Category} notification to the Inbox of user {UserId}", message.Category, userId);
            return;
        }

        feed.Publish(userId, new NotificationFeedEvent(NotificationFeedEventTypes.Notification, notification));
        await DeliverToChannelsAsync(userId, notification, ct);
    }

    private async Task DeliverToChannelsAsync(Guid userId, NotificationDto notification, CancellationToken ct)
    {
        foreach (var channel in _channels)
        {
            try
            {
                await channel.DeliverAsync(userId, notification, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Channel {Channel} failed to deliver notification {NotificationId} to user {UserId}",
                    channel.Name, notification.Id, userId);
            }
        }
    }
}
