using Sentium.Infrastructure.Security;
using Sentium.Nuntius.Contracts;
using Sentium.Nuntius.Features.Inbox;

namespace Sentium.Nuntius.Features.Publishing;

/// <summary>
/// Singleton wrapper that runs each save in its own DI scope, marked as a system scope before anything is resolved:
/// publishers are background workers with no HTTP user, so the per-user filter would otherwise hide every row.
/// </summary>
internal sealed class ScopedNotificationStore(IServiceScopeFactory scopeFactory) : INotificationStore
{
    public async Task<NotificationDto> SaveAsync(Guid userId, NotificationMessage message, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<SystemScopeContext>().Activate();
        return await scope.ServiceProvider.GetRequiredService<NotificationStore>().SaveAsync(userId, message, ct);
    }
}
