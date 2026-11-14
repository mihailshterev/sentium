namespace Sentium.Nuntius.Contracts;

/// <summary>
/// Who a Notification is for. <see cref="Sovereigns"/> and <see cref="Household"/> fan out to one Notification per
/// user at publish time.
/// </summary>
public sealed record NotificationRecipient
{
    private NotificationRecipient(NotificationAudience audience, Guid? userId)
    {
        Audience = audience;
        UserId = userId;
    }

    public static NotificationRecipient Sovereigns { get; } = new(NotificationAudience.Sovereigns, null);

    public static NotificationRecipient Household { get; } = new(NotificationAudience.Household, null);

    public NotificationAudience Audience { get; }

    /// <summary>
    /// The single recipient when <see cref="Audience"/> is <see cref="NotificationAudience.User"/>; otherwise
    /// <see langword="null"/>.
    /// </summary>
    public Guid? UserId { get; }

    public static NotificationRecipient User(Guid userId) => new(NotificationAudience.User, userId);
}
