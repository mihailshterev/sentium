using System.Text.Json;
using Sentium.Registry.Contracts;

namespace Sentium.Registry.Features.Settings;

/// <summary>
/// Cross-module entry point for <see cref="ISettingsService"/>. Every call runs in its own DI scope, so
/// singleton consumers can depend on it and concurrent callers never share a <see cref="Data.RegistryDbContext"/>.
/// Reads are served from <see cref="SettingsService"/>'s HybridCache; the database is hit only on a miss.
/// </summary>
internal sealed class ScopedSettingsService(IServiceScopeFactory scopeFactory) : ISettingsService
{
    public async Task<SettingsEnvelope?> GetAsync(string key, Guid? userId, CancellationToken ct = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<SettingsService>().GetAsync(key, userId, ct);
    }

    public async Task<SettingsEnvelope> UpdateAsync(string key, Guid? userId, JsonElement payload, string? updatedBy = null, CancellationToken ct = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<SettingsService>().UpdateAsync(key, userId, payload, updatedBy, ct);
    }
}
