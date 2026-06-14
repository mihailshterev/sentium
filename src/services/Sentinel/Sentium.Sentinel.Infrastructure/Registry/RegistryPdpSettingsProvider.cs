using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Sentium.Sentinel.Core.Settings;
using Sentium.Shared.Constants;

namespace Sentium.Sentinel.Infrastructure.Registry;

public sealed class RegistryPdpSettingsProvider(
    IHttpClientFactory httpClientFactory,
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
                    var client = httpClientFactory.CreateClient(ServiceNames.Registry);
                    var envelope = await client.GetFromJsonAsync<SettingsEnvelope<PdpSettingsResponse>>($"settings/{SettingsKeys.Pdp}", token);
                    return envelope?.Value is null ? Fallback() : Map(envelope.Value);
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
            logger.LogWarning(ex, "Failed to fetch PDP settings from Registry; using class defaults");
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

    private sealed record SettingsEnvelope<T>(string Key, T Value, DateTimeOffset UpdatedAt, string? UpdatedBy);
}
