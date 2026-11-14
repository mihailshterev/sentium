using Sentium.Core.Constants;
using Sentium.Registry.Contracts;

namespace Sentium.Vigil.Features.Settings;

/// <summary>
/// Reads the Vigil monitoring settings from the Registry module in-process. Registry caches the settings
/// envelope itself (HybridCache L1 + L2) and evicts it on change, so Vigil keeps no copy of its own.
/// </summary>
public sealed class RegistryVigilSettingsProvider(
    ISettingsService settingsService,
    ILogger<RegistryVigilSettingsProvider> logger) : IVigilSettingsProvider
{
    public async ValueTask<VigilRuntimeSettings> GetAsync(CancellationToken ct = default)
    {
        try
        {
            var settings = await settingsService.GetValueAsync<VigilSettingsResponse>(SettingsKeys.Vigil, ct: ct);
            return settings is null ? Fallback() : Map(settings);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to read Vigil settings from Registry; using class defaults");
            return Fallback();
        }
    }

    private static VigilRuntimeSettings Fallback() => new();

    private static VigilRuntimeSettings Map(VigilSettingsResponse dto)
    {
        return new()
        {
            PollIntervalSeconds = dto.PollIntervalSeconds,
            ProbeTimeoutSeconds = dto.ProbeTimeoutSeconds,
            DegradedLatencyMs = dto.DegradedLatencyMs,
            ConsecutiveFailuresToOpenIncident = dto.ConsecutiveFailuresToOpenIncident,
            SampleHistorySize = dto.SampleHistorySize
        };
    }

    private sealed record VigilSettingsResponse(
        int PollIntervalSeconds,
        int ProbeTimeoutSeconds,
        int DegradedLatencyMs,
        int ConsecutiveFailuresToOpenIncident,
        int SampleHistorySize);
}
