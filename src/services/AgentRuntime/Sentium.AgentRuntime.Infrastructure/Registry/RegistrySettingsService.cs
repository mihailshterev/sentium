using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Sentium.AgentRuntime.Core.Registry;

namespace Sentium.AgentRuntime.Infrastructure.Registry;

public sealed class RegistrySettingsService(
    IRegistryClient registryClient,
    HybridCache cache,
    ILogger<RegistrySettingsService> logger) : IRegistrySettingsService
{
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromHours(1),
        LocalCacheExpiration = TimeSpan.FromMinutes(5)
    };

    public async ValueTask<SettingsSnapshot> GetAsync(Guid? userId, CancellationToken ct = default)
    {
        try
        {
            return await cache.GetOrCreateAsync(
                AgentRuntimeSettingsCacheKeys.Snapshot(userId),
                async token =>
                {
                    var snapshot = await registryClient.GetSettingsAsync(userId, token);

                    return snapshot ?? throw new InvalidOperationException("Registry did not return settings");
                },
                CacheOptions,
                tags: [AgentRuntimeSettingsCacheKeys.SnapshotTag],
                cancellationToken: ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falling back to default settings for user {UserId} (Registry unavailable)", userId);
            return SettingsSnapshot.Default;
        }
    }
}
