using Microsoft.Extensions.Caching.Hybrid;
using Sentium.AgentRuntime.Core.Registry;
using Sentium.Shared.Constants;

namespace Sentium.AgentRuntime.Infrastructure.Registry;

public sealed class RegistrySettingsService(IRegistryClient registryClient, HybridCache cache) : IRegistrySettingsService
{
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromHours(1),
        LocalCacheExpiration = TimeSpan.FromMinutes(5)
    };

    public async ValueTask<SettingsSnapshot> GetAsync(CancellationToken ct = default)
    {
        try
        {
            return await cache.GetOrCreateAsync(
                CacheKeys.Settings,
                async token =>
                {
                    var snapshot = await registryClient.GetSettingsAsync(token);

                    return snapshot ?? throw new InvalidOperationException("Registry did not return settings");
                },
                CacheOptions,
                cancellationToken: ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return SettingsSnapshot.Default;
        }
    }
}
