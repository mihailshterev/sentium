using Microsoft.Extensions.Caching.Hybrid;
using Sentium.Core.Constants;
using Sentium.Registry.Contracts;

namespace Sentium.Sentinel.Features.Settings;

public sealed class RegistryPdpSettingsProvider(
    ISettingsService settingsService,
    HybridCache cache,
    ILogger<RegistryPdpSettingsProvider> logger) : IPdpRuntimeSettingsProvider
{
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromHours(1),
        LocalCacheExpiration = TimeSpan.FromMinutes(5)
    };

    public async ValueTask<PdpRuntimeSettings> GetAsync(CancellationToken ct = default)
    {
        try
        {
            return await cache.GetOrCreateAsync(
                SentinelSettingsCacheKeys.PdpRuntime(null),
                async token =>
                {
                    var settings = await settingsService.GetValueAsync<PdpSettingsResponse>(SettingsKeys.Pdp, ct: token);
                    return settings is null ? Fallback() : Map(settings);
                },
                CacheOptions,
                cancellationToken: ct
            );
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read PDP settings from Registry; using class defaults");
            return Fallback();
        }
    }

    private static PdpRuntimeSettings Fallback() => new()
    {
        LockdownMode = false,
        AutonomyLevel = 5,
        SemanticIntentCheckEnabled = true,
        IntentCheckModel = string.Empty,
        RateLimitMaxRequests = 120,
        RateLimitWindowSeconds = 60
    };

    private static PdpRuntimeSettings Map(PdpSettingsResponse dto) => new()
    {
        LockdownMode = dto.LockdownMode,
        AutonomyLevel = dto.AutonomyLevel,
        SemanticIntentCheckEnabled = dto.SemanticIntentCheckEnabled,
        IntentCheckModel = dto.IntentCheckModel,
        RateLimitMaxRequests = dto.RateLimitMaxRequests,
        RateLimitWindowSeconds = dto.RateLimitWindowSeconds
    };

    private sealed record PdpSettingsResponse(
        bool LockdownMode,
        int AutonomyLevel,
        bool SemanticIntentCheckEnabled,
        string IntentCheckModel,
        int RateLimitMaxRequests,
        int RateLimitWindowSeconds);
}
