using System.Text.Json;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Sentium.Infrastructure.Messaging;
using Sentium.Registry.Core.Entities;
using Sentium.Registry.Core.Settings;
using Sentium.Shared.Constants;
using Sentium.Shared.Events;

namespace Sentium.Registry.Application.Settings;

public sealed class SettingsService(
    ISettingsRepository repository,
    ISettingsCatalog catalog,
    HybridCache cache,
    IEventBus eventBus,
    IServiceProvider serviceProvider,
    ILogger<SettingsService> logger) : ISettingsService
{
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromHours(1),
        LocalCacheExpiration = TimeSpan.FromMinutes(5)
    };

    public async Task<SettingsEnvelope?> GetAsync(string key, Guid? userId, CancellationToken ct = default)
    {
        if (!catalog.TryGet(key, out var descriptor))
        {
            return null;
        }

        var scopeUserId = ScopeUserId(descriptor, userId);

        return await cache.GetOrCreateAsync(
            CacheKeys.SettingsFor(descriptor.Key, scopeUserId),
            async token => await LoadAsync(descriptor, scopeUserId, token),
            CacheOptions,
            cancellationToken: ct
        );
    }

    public async Task<SettingsEnvelope> UpdateAsync(string key, Guid? userId, JsonElement payload, string? updatedBy = null, CancellationToken ct = default)
    {
        if (!catalog.TryGet(key, out var descriptor))
        {
            throw new KeyNotFoundException($"Unknown settings key '{key}'.");
        }

        var scopeUserId = ScopeUserId(descriptor, userId);
        var value = descriptor.Deserialize(payload);
        await descriptor.ValidateAsync(value, serviceProvider, ct);

        var isNew = false;
        var entity = await repository.FindAsync(scopeUserId, ct);
        if (entity is null)
        {
            entity = new SystemSettings { UserId = scopeUserId };
            isNew = true;
        }

        descriptor.Write(entity.Settings, value);
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.UpdatedBy = updatedBy;

        if (isNew)
        {
            await repository.AddAsync(entity, ct);
        }
        else
        {
            await repository.UpdateAsync(entity, ct);
        }

        var cacheKey = CacheKeys.SettingsFor(descriptor.Key, scopeUserId);
        await cache.RemoveAsync(cacheKey, ct);
        await eventBus.PublishAsync(NatsSubjects.SettingsInvalidated, new SettingsInvalidatedEvent(cacheKey, DateTimeOffset.UtcNow), ct: ct);

        logger.LogInformation("Settings '{Key}' updated for {Scope} by {By}; cache invalidated", descriptor.Key, scopeUserId?.ToString() ?? "global", updatedBy ?? "system");

        return new SettingsEnvelope(descriptor.Key, value, entity.UpdatedAt, entity.UpdatedBy);
    }

    private async Task<SettingsEnvelope> LoadAsync(ISettingsDescriptor descriptor, Guid? scopeUserId, CancellationToken ct)
    {
        var entity = await repository.FindAsync(scopeUserId, ct);
        var container = entity?.Settings ?? new SettingsContainer();
        var value = descriptor.Read(container);
        return new SettingsEnvelope(descriptor.Key, value, entity?.UpdatedAt ?? DateTimeOffset.UtcNow, entity?.UpdatedBy);
    }

    private static Guid? ScopeUserId(ISettingsDescriptor descriptor, Guid? userId) => descriptor.Scope == SettingsScope.Global ? null : userId;
}
