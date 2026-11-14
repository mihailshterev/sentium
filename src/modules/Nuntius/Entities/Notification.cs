using Sentium.Core.Common;
using Sentium.Nuntius.Contracts;

namespace Sentium.Nuntius.Entities;

/// <summary>
/// One message to one user, kept in their Inbox until the retention sweep removes it.
/// </summary>
internal sealed class Notification : IUserOwned
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Category { get; set; } = null!;

    public NotificationSeverity Severity { get; set; }

    public string Title { get; set; } = null!;

    public string? Body { get; set; }

    public string? Link { get; set; }

    public string? DedupeKey { get; set; }

    /// <summary>
    /// How many publishes this Notification absorbed through its dedupe key (1 for a fresh one).
    /// </summary>
    public int Occurrences { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// The last publish (the creation, or a deduplicated repeat); the Inbox is ordered and swept by it.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? ReadAt { get; set; }
}
