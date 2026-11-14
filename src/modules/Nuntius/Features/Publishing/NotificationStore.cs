using Microsoft.EntityFrameworkCore;
using Sentium.Nuntius.Common;
using Sentium.Nuntius.Contracts;
using Sentium.Nuntius.Data;
using Sentium.Nuntius.Entities;
using Sentium.Nuntius.Features.Inbox;

namespace Sentium.Nuntius.Features.Publishing;

/// <summary>
/// Writes Notifications for explicitly named users. Must run in a system scope (see
/// <see cref="ScopedNotificationStore"/>), so ownership is stamped from the recipient, never from the caller.
/// </summary>
internal sealed class NotificationStore(NuntiusDbContext db, TimeProvider timeProvider) : INotificationStore
{
    public async Task<NotificationDto> SaveAsync(Guid userId, NotificationMessage message, CancellationToken ct)
    {
        var normalized = Normalize(message);
        var notification = normalized.DedupeKey is null
            ? await InsertAsync(userId, normalized, ct)
            : await UpsertAsync(userId, normalized, normalized.DedupeKey, ct);

        return InboxMapping.ToDto(notification);
    }

    private async Task<Notification> InsertAsync(Guid userId, NotificationMessage message, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var notification = new Notification
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Category = message.Category,
            Severity = message.Severity,
            Title = message.Title,
            Body = message.Body,
            Link = message.Link,
            Occurrences = 1,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Notifications.Add(notification);
        await db.SaveChangesAsync(ct);
        return notification;
    }

    /// <summary>
    /// Inserts, or folds into the user's unread Notification with the same key, in one statement against the partial
    /// unique index, so concurrent publishes of one key never create a duplicate or lose a repeat.
    /// </summary>
    private async Task<Notification> UpsertAsync(Guid userId, NotificationMessage message, string dedupeKey, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var severity = message.Severity.ToString();

        // The query filter would wrap this statement in a SELECT subquery, which an INSERT cannot be. Skipping it is safe:
        // the statement only ever touches userId's row. The table name is literal because FromSql parameterizes every
        // interpolated value, and an identifier cannot be a parameter.
        var rows = await db.Notifications
            .FromSql($"""
                INSERT INTO nuntius."Notifications"
                    ("Id", "UserId", "Category", "Severity", "Title", "Body", "Link", "DedupeKey", "Occurrences", "CreatedAt", "UpdatedAt", "ReadAt")
                VALUES ({Guid.CreateVersion7()}, {userId}, {message.Category}, {severity}, {message.Title}, {message.Body}, {message.Link}, {dedupeKey}, 1, {now}, {now}, NULL)
                ON CONFLICT ("UserId", "DedupeKey") WHERE "ReadAt" IS NULL AND "DedupeKey" IS NOT NULL
                DO UPDATE SET
                    "Category" = EXCLUDED."Category",
                    "Severity" = EXCLUDED."Severity",
                    "Title" = EXCLUDED."Title",
                    "Body" = EXCLUDED."Body",
                    "Link" = EXCLUDED."Link",
                    "Occurrences" = "Notifications"."Occurrences" + 1,
                    "UpdatedAt" = EXCLUDED."UpdatedAt"
                RETURNING *
                """)
            .IgnoreQueryFilters()
            .AsNoTracking()
            .ToListAsync(ct);

        return rows.Single();
    }

    private static NotificationMessage Normalize(NotificationMessage message)
    {
        return message with
        {
            Category = NuntiusLimits.Truncate(message.Category.Trim(), NuntiusLimits.CategoryMax),
            Title = NuntiusLimits.Truncate(message.Title.Trim(), NuntiusLimits.TitleMax),
            Body = NuntiusLimits.TruncateOrNull(message.Body, NuntiusLimits.BodyMax),
            Link = NuntiusLimits.TruncateOrNull(message.Link, NuntiusLimits.LinkMax),
            DedupeKey = NuntiusLimits.TruncateOrNull(message.DedupeKey, NuntiusLimits.DedupeKeyMax)
        };
    }
}
