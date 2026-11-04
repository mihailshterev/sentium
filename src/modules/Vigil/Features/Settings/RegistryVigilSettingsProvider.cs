using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Sentium.Core.Constants;
using Sentium.Registry.Contracts;

namespace Sentium.Vigil.Features.Settings;

public sealed class RegistryVigilSettingsProvider(
    ISettingsService settingsService,
    HybridCache cache,
    ILogger<RegistryVigilSettingsProvider> logger) : IVigilSettingsProvider
{
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromHours(1),
        LocalCacheExpiration = TimeSpan.FromMinutes(5)
    };

    public async ValueTask<VigilRuntimeSettings> GetAsync(CancellationToken ct = default)
    {
        try
        {
            return await cache.GetOrCreateAsync(
                VigilSettingsCacheKeys.Runtime(null),
                async token =>
                {
                    var settings = await settingsService.GetValueAsync<VigilSettingsResponse>(SettingsKeys.Vigil, ct: token);
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
            logger.LogWarning(ex, "Failed to read Vigil settings from Registry; using class defaults");
            return Fallback();
        }
    }

    private static VigilRuntimeSettings Fallback() => new();

    private static VigilRuntimeSettings Map(VigilSettingsResponse dto) => new()
    {
        PollIntervalSeconds = dto.PollIntervalSeconds,
        ProbeTimeoutSeconds = dto.ProbeTimeoutSeconds,
        DegradedLatencyMs = dto.DegradedLatencyMs,
        ConsecutiveFailuresToOpenIncident = dto.ConsecutiveFailuresToOpenIncident,
        SampleHistorySize = dto.SampleHistorySize
    };

    private sealed record VigilSettingsResponse(
        int PollIntervalSeconds,
        int ProbeTimeoutSeconds,
        int DegradedLatencyMs,
        int ConsecutiveFailuresToOpenIncident,
        int SampleHistorySize);
}
