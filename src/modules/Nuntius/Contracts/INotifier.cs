namespace Sentium.Nuntius.Contracts;

/// <summary>
/// Tells people something. Every Notification lands in the recipient's Inbox (and is pushed live to their open
/// portal sessions); additional Channels may deliver it too. Callers are usually background workers, so recipients
/// are named explicitly and nothing relies on an HTTP user.
/// </summary>
public interface INotifier
{
    /// <summary>
    /// Publishes <paramref name="message"/> to every user <paramref name="recipient"/> resolves to, one Notification
    /// each. Never throws into the caller: a failure is logged, and a failed Channel never undoes the Inbox write.
    /// </summary>
    Task NotifyAsync(NotificationRecipient recipient, NotificationMessage message, CancellationToken ct = default);
}
