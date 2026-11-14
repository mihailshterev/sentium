namespace Sentium.Nuntius.Contracts;

/// <summary>
/// The content of a Notification. Over-long fields are truncated, never rejected.
/// </summary>
/// <param name="Category">One of <see cref="NotificationCategories"/>.</param>
/// <param name="Link">A portal path (for example <c>/vigil</c>) the Notification opens.</param>
/// <param name="DedupeKey">
/// While the recipient still has an unread Notification with this key, publishing again updates that one instead of
/// adding a second, so a flapping source cannot flood the Inbox.
/// </param>
public sealed record NotificationMessage(
    string Category,
    NotificationSeverity Severity,
    string Title,
    string? Body = null,
    string? Link = null,
    string? DedupeKey = null);
