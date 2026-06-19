using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Sentium.Shared.Auditing;

namespace Sentium.Infrastructure.Interceptors;

public class AuditInterceptor(ILogger<AuditInterceptor> logger) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        LogAudit(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        LogAudit(eventData.Context);
        return base.SavingChangesAsync(eventData, result, ct);
    }

    private void LogAudit(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        if (!logger.IsEnabled(LogLevel.Information))
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
            logger.LogInformation(
                "AUDIT: {Action} on {Entity} (key={Key}) changed [{ChangedProperties}]",
                entry.Action, entry.Entity, entry.Key, string.Join(", ", entry.ChangedProperties));
        }
    }
}
