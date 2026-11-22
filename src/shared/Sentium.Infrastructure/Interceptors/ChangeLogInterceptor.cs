using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Sentium.Core.Common;

namespace Sentium.Infrastructure.Interceptors;

/// <summary>
/// Logs, at Debug level, which entities a save adds, changes or deletes - a troubleshooting aid, not an audit trail
/// (Sentinel keeps that). Entities marked <see cref="INonAudited"/> are left out.
/// </summary>
internal sealed class ChangeLogInterceptor(ILogger<ChangeLogInterceptor> logger) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        LogChanges(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        LogChanges(eventData.Context);
        return base.SavingChangesAsync(eventData, result, ct);
    }

    private void LogChanges(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        if (!logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        var entries = context.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Where(e => e.Entity is not INonAudited)
            .Select(e => new
            {
                Entity = e.Entity.GetType().Name,
                Action = e.State.ToString(),
                Key = string.Join(
                    ",",
                    e.Properties.Where(p => p.Metadata.IsPrimaryKey()).Select(p => p.CurrentValue?.ToString() ?? "null")),
                ChangedProperties = e.Properties
                    .Where(p => p.IsModified || e.State == EntityState.Added)
                    .Select(p => p.Metadata.Name)
                    .ToArray()
            })
            .Where(x => x.ChangedProperties.Length > 0)
            .ToList();

        foreach (var entry in entries)
        {
            logger.LogDebug(
                "{Action} on {Entity} (key={Key}) changed [{ChangedProperties}]",
                entry.Action, entry.Entity, entry.Key, string.Join(", ", entry.ChangedProperties));
        }
    }
}
