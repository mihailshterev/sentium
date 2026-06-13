using Microsoft.EntityFrameworkCore;
using Sentium.Registry.Core.Entities;
using Sentium.Registry.Core.Settings;
using Sentium.Registry.Infrastructure.Data;

namespace Sentium.Registry.Infrastructure.Settings;

public sealed class SettingsRepository(RegistryDbContext db) : ISettingsRepository
{
    public Task<SystemSettings?> FindAsync(Guid? userId, CancellationToken ct = default)
        => db.SystemSettings.FirstOrDefaultAsync(s => s.UserId == userId, ct);

    public async Task AddAsync(SystemSettings entity, CancellationToken ct = default)
    {
        db.SystemSettings.Add(entity);
        await db.SaveChangesAsync(ct);
    }

    public Task UpdateAsync(SystemSettings entity, CancellationToken ct = default)
        => db.SystemSettings
            .Where(s => s.Id == entity.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.Settings, entity.Settings)
                .SetProperty(s => s.UpdatedAt, entity.UpdatedAt)
                .SetProperty(s => s.UpdatedBy, entity.UpdatedBy), ct);
}
