using Sentium.AgentRuntime.Core.Entities;
using Sentium.AgentRuntime.Core.Settings;
using Sentium.AgentRuntime.Infrastructure.Data;

namespace Sentium.AgentRuntime.Infrastructure.Settings;

public sealed class SystemSettingsRepository(AgentRuntimeDbContext context) : ISystemSettingsRepository
{
    public async Task<SystemSettings?> FindAsync(CancellationToken ct = default)
        => await context.SystemSettings.FindAsync([SystemSettings.WellKnownId], ct);

    public async Task AddAsync(SystemSettings entity, CancellationToken ct = default)
    {
        context.SystemSettings.Add(entity);
        await context.SaveChangesAsync(ct);
    }

    public async Task SaveAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
