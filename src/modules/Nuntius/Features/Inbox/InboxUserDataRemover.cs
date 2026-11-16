using Microsoft.EntityFrameworkCore;
using Sentium.Identity.Contracts;
using Sentium.Infrastructure.Security;
using Sentium.Nuntius.Data;

namespace Sentium.Nuntius.Features.Inbox;

/// <summary>
/// Deletes a removed user's Inbox.
/// </summary>
internal sealed class InboxUserDataRemover(IServiceScopeFactory scopeFactory) : IUserRemovalObserver
{
    public async Task OnUserRemovedAsync(Guid userId, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<SystemScopeContext>().Activate();
        var db = scope.ServiceProvider.GetRequiredService<NuntiusDbContext>();

        await db.Notifications.IgnoreQueryFilters().Where(n => n.UserId == userId).ExecuteDeleteAsync(ct);
    }
}
