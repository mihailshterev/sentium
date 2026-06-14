using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Sentium.Shared.Constants;
using Sentium.Watchdog.Core.Settings;

namespace Sentium.Watchdog.Application.Settings;

public sealed class RegistryWatchdogSettingsProvider(
    IHttpClientFactory httpClientFactory,
    HybridCache cache,
    ILogger<RegistryWatchdogSettingsProvider> logger) : IWatchdogSettingsProvider
{
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromHours(1),
        LocalCacheExpiration = TimeSpan.FromMinutes(5)
    };

    public async ValueTask<WatchdogRuntimeSettings> GetAsync(CancellationToken ct = default)
    {
        try
        {
            return await cache.GetOrCreateAsync(
                WatchdogSettingsCacheKeys.Runtime(null),
                async token =>
                {
                    var client = httpClientFactory.CreateClient(ServiceNames.Registry);
                    var envelope = await client.GetFromJsonAsync<SettingsEnvelope<WatchdogSettingsResponse>>($"settings/{SettingsKeys.Watchdog}", token);
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
            logger.LogWarning(ex, "Failed to fetch Watchdog settings from Registry; using class defaults");
            return Fallback();
        }
    }

    private static WatchdogRuntimeSettings Fallback() => new();

    private static WatchdogRuntimeSettings Map(WatchdogSettingsResponse dto) => new()
    {
        PollIntervalSeconds = dto.PollIntervalSeconds,
        ProbeTimeoutSeconds = dto.ProbeTimeoutSeconds,
        DegradedLatencyMs = dto.DegradedLatencyMs,
        ConsecutiveFailuresToOpenIncident = dto.ConsecutiveFailuresToOpenIncident,
        SampleHistorySize = dto.SampleHistorySize
    };

    private sealed record WatchdogSettingsResponse(
        int PollIntervalSeconds,
        int ProbeTimeoutSeconds,
        int DegradedLatencyMs,
        int ConsecutiveFailuresToOpenIncident,
        int SampleHistorySize);

    private sealed record SettingsEnvelope<T>(string Key, T Value, DateTimeOffset UpdatedAt, string? UpdatedBy);
}
