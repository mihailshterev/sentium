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

    public ValueTask<SettingsSnapshot> GetAsync(CancellationToken ct = default)
        => cache.GetOrCreateAsync(
            CacheKeys.Settings,
            async token =>
            {
                var snapshot = await registryClient.GetSettingsAsync(token);
                return snapshot ?? SettingsSnapshot.Default;
            },
            CacheOptions,
            cancellationToken: ct);
}
