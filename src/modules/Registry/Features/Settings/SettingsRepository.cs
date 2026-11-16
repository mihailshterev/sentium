using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sentium.Registry.Data;
using Sentium.Registry.Entities;

namespace Sentium.Registry.Features.Settings;

internal sealed class SettingsRepository(RegistryDbContext db) : ISettingsRepository
{
    public Task<SystemSettings?> FindAsync(Guid? userId, CancellationToken ct = default)
        => db.SystemSettings.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == userId, ct);

    public async Task<bool> TryAddAsync(SystemSettings entity, CancellationToken ct = default)
    {
        db.SystemSettings.Add(entity);

        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.ChangeTracker.Clear();
            return false;
        }
    }

    public async Task<bool> TryUpdateAsync(SystemSettings entity, CancellationToken ct = default)
    {
        var updated = await db.SystemSettings
            .Where(s => s.Id == entity.Id && s.Version == entity.Version)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.Settings, entity.Settings)
                .SetProperty(s => s.UpdatedAt, entity.UpdatedAt)
                .SetProperty(s => s.UpdatedBy, entity.UpdatedBy), ct);

        return updated == 1;
    }

    public Task RemoveAsync(Guid userId, CancellationToken ct = default)
        => db.SystemSettings.Where(s => s.UserId == userId).ExecuteDeleteAsync(ct);
}
