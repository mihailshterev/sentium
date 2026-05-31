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
    HybridCache cache,
    IEventBus eventBus,
    ILogger<SettingsService> logger) : ISettingsService
{
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromHours(1),
        LocalCacheExpiration = TimeSpan.FromMinutes(5)
    };

    public ValueTask<SettingsDto> GetAsync(CancellationToken ct = default)
        => cache.GetOrCreateAsync(
            CacheKeys.Settings,
            Factory,
            CacheOptions,
            cancellationToken: ct);

    public async Task UpdateAsync(UpdateSettingsRequest request, string? updatedBy = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entity = await repository.FindAsync(ct);
        if (entity is null)
        {
            entity = new SystemSettings();
            await repository.AddAsync(entity, ct);
        }

        entity.Settings.Harness.UserHarnessPrompt = request.Harness.UserHarnessPrompt ?? string.Empty;
        entity.Settings.Harness.IsBuiltInHarnessEnabled = request.Harness.IsBuiltInHarnessEnabled;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.UpdatedBy = updatedBy;

        await repository.SaveAsync(ct);
        await cache.RemoveAsync(CacheKeys.Settings, ct);
        await eventBus.PublishAsync(
            NatsSubjects.SettingsInvalidated,
            new SettingsInvalidatedEvent(CacheKeys.Settings, DateTimeOffset.UtcNow),
            ct: ct);

        logger.LogInformation("Settings updated by {User}; cache invalidated", updatedBy ?? "system");
    }

    private async ValueTask<SettingsDto> Factory(CancellationToken ct)
    {
        var entity = await repository.FindAsync(ct);
        if (entity is null)
        {
            entity = new SystemSettings();
            await repository.AddAsync(entity, ct);
        }
        return MapToDto(entity);
    }

    private static SettingsDto MapToDto(SystemSettings entity) => new(
        Harness: new HarnessSettingsDto(
            entity.Settings.Harness.UserHarnessPrompt,
            entity.Settings.Harness.IsBuiltInHarnessEnabled),
        UpdatedAt: entity.UpdatedAt,
        UpdatedBy: entity.UpdatedBy);
}
