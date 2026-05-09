using Sentium.AgentRuntime.Core.Entities;
using Sentium.AgentRuntime.Core.Settings;
using Microsoft.Extensions.Caching.Hybrid;

namespace Sentium.AgentRuntime.Infrastructure.Settings;

public sealed class SystemSettingsService(ISystemSettingsRepository repository, HybridCache cache) : ISystemSettingsService
{
    private const string CacheKey = "system:settings";
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromSeconds(30),
        LocalCacheExpiration = TimeSpan.FromSeconds(30)
    };

    public async Task<SystemSettingsDto> GetAsync(CancellationToken ct = default)
    {
        return await cache.GetOrCreateAsync(CacheKey, async token =>
            {
                var entity = await repository.FindAsync(token);

                if (entity is null)
                {
                    entity = new SystemSettings();
                    await repository.AddAsync(entity, token);
                }

                return MapToDto(entity);
            },
            CacheOptions,
            cancellationToken: ct
        );
    }

    public async Task UpdateAsync(UpdateSystemSettingsRequest request, string? updatedBy = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entity = await repository.FindAsync(ct);

        if (entity is null)
        {
            entity = new SystemSettings();
            await repository.AddAsync(entity, ct);
        }

        entity.UserHarnessPrompt = request.UserHarnessPrompt ?? string.Empty;
        entity.IsBuiltInHarnessEnabled = request.IsBuiltInHarnessEnabled;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.UpdatedBy = updatedBy;

        await repository.SaveAsync(ct);
        await cache.RemoveAsync(CacheKey, ct);
    }

    private static SystemSettingsDto MapToDto(SystemSettings entity)
        => new(entity.UserHarnessPrompt, entity.IsBuiltInHarnessEnabled, entity.UpdatedAt, entity.UpdatedBy);
}
