namespace Sentium.Nuntius.Features.Inbox;

/// <summary>
/// The signed-in user's own Inbox; the DbContext filter keeps every query to that user, Sovereigns included.
/// </summary>
internal interface IInboxService
{
    /// <summary>
    /// The newest <paramref name="limit"/> Notifications, by last publish.
    /// </summary>
    Task<IReadOnlyList<NotificationDto>> ListAsync(bool unreadOnly, int limit, CancellationToken ct);

    Task<int> CountUnreadAsync(CancellationToken ct);

    /// <summary>
    /// Returns <see langword="false"/> when the Notification does not exist in the caller's Inbox; marking an already
    /// read one is a no-op that still succeeds.
    /// </summary>
    Task<bool> MarkReadAsync(Guid id, CancellationToken ct);

    /// <returns>How many Notifications were unread.</returns>
    Task<int> MarkAllReadAsync(CancellationToken ct);
}
