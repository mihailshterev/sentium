using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Caching.Hybrid;
using Sentium.Registry.Contracts;
using Sentium.Registry.Contracts.Settings;
using Sentium.Registry.Entities;

namespace Sentium.Registry.Features.Settings;

internal sealed class SettingsService(
    ISettingsRepository repository,
    ISettingsCatalog catalog,
    HybridCache cache,
    IServiceProvider serviceProvider,
    ILogger<SettingsService> logger) : ISettingsService
{
    private const int MaxWriteAttempts = 3;

    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    // Every write evicts the entry, so the expiration only bounds memory, not staleness.
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromHours(1)
    };

    public async Task<SettingsEnvelope?> GetAsync(string key, Guid? userId, CancellationToken ct = default)
    {
        if (!catalog.TryGet(key, out var descriptor))
        {
            return null;
        }

        var scopeUserId = ScopeUserId(descriptor, userId);

        return await cache.GetOrCreateAsync(
            RegistrySettingsCacheKeys.Envelope(descriptor.Key, scopeUserId),
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

        SystemSettings entity;
        object value;
        var attempt = 0;

        // Optimistic: every key shares one row, so a concurrent update of another key makes this write lose; it then
        // re-reads the row and merges its own change into the newer settings.
        while (true)
        {
            attempt++;

            var existing = await repository.FindAsync(scopeUserId, ct);
            entity = existing ?? new SystemSettings { UserId = scopeUserId };

            var merged = MergePayload(descriptor.Read(entity.Settings), payload);
            value = descriptor.Deserialize(merged);
            await descriptor.ValidateAsync(value, serviceProvider, ct);

            descriptor.Write(entity.Settings, value);
            entity.UpdatedAt = DateTimeOffset.UtcNow;
            entity.UpdatedBy = updatedBy;

            var saved = existing is null
                ? await repository.TryAddAsync(entity, ct)
                : await repository.TryUpdateAsync(entity, ct);

            if (saved)
            {
                break;
            }

            if (attempt == MaxWriteAttempts)
            {
                throw new InvalidOperationException($"Settings '{descriptor.Key}' kept changing concurrently; the update was not saved.");
            }
        }

        // Consumers read through this cache entry and keep no copies, so evicting it is the whole invalidation.
        await cache.RemoveAsync(RegistrySettingsCacheKeys.Envelope(descriptor.Key, scopeUserId), ct);

        logger.LogInformation("Settings '{Key}' updated for {Scope} by {By}; cache invalidated", descriptor.Key, scopeUserId?.ToString() ?? "global", updatedBy ?? "system");

        await NotifyHandlersAsync(descriptor.Key, scopeUserId, ct);

        return new SettingsEnvelope(descriptor.Key, JsonSerializer.SerializeToElement(value, value.GetType(), WebOptions), entity.UpdatedAt, entity.UpdatedBy);
    }

    public async Task RemoveUserSettingsAsync(Guid userId, CancellationToken ct = default)
    {
        await repository.RemoveAsync(userId, ct);

        foreach (var descriptor in catalog.All.Where(d => d.Scope == SettingsScope.Personal))
        {
            await cache.RemoveAsync(RegistrySettingsCacheKeys.Envelope(descriptor.Key, userId), ct);
        }
    }

    private async Task NotifyHandlersAsync(string key, Guid? scopeUserId, CancellationToken ct)
    {
        foreach (var handler in serviceProvider.GetService<IEnumerable<ISettingsChangedHandler>>() ?? [])
        {
            try
            {
                await handler.OnSettingsChangedAsync(key, scopeUserId, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Settings-changed handler {Handler} failed for '{Key}'", handler.GetType().Name, key);
            }
        }
    }

    private static JsonElement MergePayload(object current, JsonElement patch)
    {
        if (JsonSerializer.SerializeToNode(current, current.GetType(), WebOptions) is not JsonObject node)
        {
            return patch;
        }

        foreach (var prop in patch.EnumerateObject())
        {
            node[prop.Name] = prop.Value.Deserialize<JsonNode>(WebOptions);
        }

        return node.Deserialize<JsonElement>(WebOptions);
    }

    private async Task<SettingsEnvelope> LoadAsync(ISettingsDescriptor descriptor, Guid? scopeUserId, CancellationToken ct)
    {
        var entity = await repository.FindAsync(scopeUserId, ct);
        var value = descriptor.Read(entity?.Settings ?? new Dictionary<string, JsonElement>());
        return new SettingsEnvelope(descriptor.Key, JsonSerializer.SerializeToElement(value, value.GetType(), WebOptions), entity?.UpdatedAt ?? DateTimeOffset.UtcNow, entity?.UpdatedBy);
    }

    private static Guid? ScopeUserId(ISettingsDescriptor descriptor, Guid? userId) => descriptor.Scope == SettingsScope.Household ? null : userId;
}
