using Microsoft.EntityFrameworkCore;
using Sentium.Registry.Core.Entities;
using Sentium.Registry.Core.Settings;
using Sentium.Registry.Infrastructure.Data;

namespace Sentium.Registry.Infrastructure.Settings;

public sealed class SettingsRepository(RegistryDbContext db) : ISettingsRepository
{
    public Task<SystemSettings?> FindAsync(CancellationToken ct = default)
        => db.SystemSettings.FirstOrDefaultAsync(ct);

    public async Task AddAsync(SystemSettings entity, CancellationToken ct = default)
    {
        db.SystemSettings.Add(entity);
        await db.SaveChangesAsync(ct);
    }

    public Task SaveAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);
}
