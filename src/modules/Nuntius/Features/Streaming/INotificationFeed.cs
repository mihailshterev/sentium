namespace Sentium.Nuntius.Features.Streaming;

/// <summary>
/// Live per-user fan-out to open Device streams and, through the realtime stream, portal sessions. Events for users
/// with nothing open are discarded; clients fetch the unread count by REST on connect, so a missed event costs nothing.
/// </summary>
internal interface INotificationFeed
{
    void Publish(Guid userId, NotificationFeedEvent evt);

    /// <summary>
    /// Starts receiving <paramref name="userId"/>'s events from now on; dispose the subscription to stop.
    /// </summary>
    NotificationFeedSubscription Subscribe(Guid userId);
}
