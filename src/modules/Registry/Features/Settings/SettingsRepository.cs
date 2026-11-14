using Microsoft.EntityFrameworkCore;
using Sentium.Registry.Entities;
using Sentium.Registry.Data;

namespace Sentium.Registry.Features.Settings;

internal sealed class SettingsRepository(RegistryDbContext db) : ISettingsRepository
{
    public Task<SystemSettings?> FindAsync(Guid? userId, CancellationToken ct = default)
        => db.SystemSettings.FirstOrDefaultAsync(s => s.UserId == userId, ct);

    public async Task AddAsync(SystemSettings entity, CancellationToken ct = default)
    {
        db.SystemSettings.Add(entity);
        await db.SaveChangesAsync(ct);
    }

    public Task UpdateAsync(SystemSettings entity, CancellationToken ct = default)
    {
        return db.SystemSettings
            .Where(s => s.Id == entity.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.Settings, entity.Settings)
                .SetProperty(s => s.UpdatedAt, entity.UpdatedAt)
                .SetProperty(s => s.UpdatedBy, entity.UpdatedBy), ct);
    }
}
