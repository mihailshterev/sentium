using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Sentium.Infrastructure.Interceptors;

public class AuditInterceptor(ILogger<AuditInterceptor> logger) : SaveChangesInterceptor
{
    private static readonly string[] SensitiveProperties =
    [
        "PasswordHash",
        "SecurityStamp",
        "ConcurrencyStamp",
        "TwoFactorEnabled",
        "PhoneNumber",
        "Token",
        "Secret"
    ];

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

        var entries = context.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            // Excluding agent conversation messages
            .Where(e => e.Entity.GetType().Name != "Message")
            .Select(e => new
            {
                Entity = e.Entity.GetType().Name,
                Action = e.State.ToString(),
                Changes = e.Properties
                    .Where(p => p.IsModified || e.State == EntityState.Added)
                    .Where(p => !SensitiveProperties.Contains(p.Metadata.Name))
                    .ToDictionary(
                        p => p.Metadata.Name,
                        p => new { Old = p.OriginalValue, New = p.CurrentValue }
                    )
            })
            .Where(x => x.Changes.Count > 0)
            .ToList();

        foreach (var entry in entries)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("AUDIT: {Action} on {Entity} {@Details}", entry.Action, entry.Entity, entry);
            }
        }
    }
}
