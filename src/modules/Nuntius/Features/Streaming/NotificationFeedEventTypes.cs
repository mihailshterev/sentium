namespace Sentium.Nuntius.Features.Streaming;

internal static class NotificationFeedEventTypes
{
    public const string Connected = "connected";

    /// <summary>
    /// A Notification was added or updated; the frame carries it.
    /// </summary>
    public const string Notification = "notification";

    /// <summary>
    /// Notifications were marked read (possibly in another session); clients refetch their unread count.
    /// </summary>
    public const string Read = "read";
}
