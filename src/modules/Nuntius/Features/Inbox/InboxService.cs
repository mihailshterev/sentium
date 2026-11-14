using Microsoft.EntityFrameworkCore;
using Sentium.Infrastructure.Security;
using Sentium.Nuntius.Common;
using Sentium.Nuntius.Data;
using Sentium.Nuntius.Features.Streaming;

namespace Sentium.Nuntius.Features.Inbox;

internal sealed class InboxService(
    NuntiusDbContext db,
    ICurrentUser currentUser,
    INotificationFeed feed,
    TimeProvider timeProvider) : IInboxService
{
    public async Task<IReadOnlyList<NotificationDto>> ListAsync(bool unreadOnly, int limit, CancellationToken ct)
    {
        var query = db.Notifications.AsNoTracking();

        if (unreadOnly)
        {
            query = query.Where(n => n.ReadAt == null);
        }

        return await query
            .OrderByDescending(n => n.UpdatedAt)
            .ThenByDescending(n => n.Id)
            .Take(Math.Clamp(limit, 1, NuntiusLimits.MaxPageSize))
            .Select(InboxMapping.Projection)
            .ToListAsync(ct);
    }

    public Task<int> CountUnreadAsync(CancellationToken ct) => db.Notifications.CountAsync(n => n.ReadAt == null, ct);

    public async Task<bool> MarkReadAsync(Guid id, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var updated = await db.Notifications
            .Where(n => n.Id == id && n.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, now), ct);

        if (updated > 0)
        {
            PublishRead();
            return true;
        }

        return await db.Notifications.AnyAsync(n => n.Id == id, ct);
    }

    public async Task<int> MarkAllReadAsync(CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var updated = await db.Notifications
            .Where(n => n.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, now), ct);

        if (updated > 0)
        {
            PublishRead();
        }

        return updated;
    }

    private void PublishRead()
    {
        if (currentUser.UserId is { } userId)
        {
            feed.Publish(userId, new NotificationFeedEvent(NotificationFeedEventTypes.Read));
        }
    }
}
