using Microsoft.EntityFrameworkCore;
using Sentium.Identity.Contracts;
using Sentium.Infrastructure.Security;
using Sentium.Tempus.Data;

namespace Sentium.Tempus.Features.Overview;

/// <summary>
/// Deletes a removed user's whole timeline: proposals, notes, tasks and series.
/// </summary>
internal sealed class TempusUserDataRemover(IServiceScopeFactory scopeFactory, ILogger<TempusUserDataRemover> logger) : IUserRemovalObserver
{
    public async Task OnUserRemovedAsync(Guid userId, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<SystemScopeContext>().Activate();
        var db = scope.ServiceProvider.GetRequiredService<TempusDbContext>();

        var deleted =
            await db.TimelineProposals.IgnoreQueryFilters().Where(p => p.UserId == userId).ExecuteDeleteAsync(ct)
            + await db.TimelineNotes.IgnoreQueryFilters().Where(n => n.UserId == userId).ExecuteDeleteAsync(ct)
            + await db.TimelineTasks.IgnoreQueryFilters().Where(t => t.UserId == userId).ExecuteDeleteAsync(ct)
            + await db.TimelineSeries.IgnoreQueryFilters().Where(s => s.UserId == userId).ExecuteDeleteAsync(ct);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Deleted {Count} timeline records of removed user {UserId}.", deleted, userId);
        }
    }
}
