using Sentium.Core.Constants;
using Sentium.Registry.Contracts;
using Sentium.Registry.Contracts.Settings;

namespace Sentium.Vigil.Features.Settings;

/// <summary>
/// Reads the Vigil monitoring settings from the Registry module in-process. Registry caches the settings
/// envelope itself (HybridCache L1 + L2) and evicts it on change, so Vigil keeps no copy of its own.
/// </summary>
internal sealed class RegistryVigilSettingsProvider(
    ISettingsService settingsService,
    ILogger<RegistryVigilSettingsProvider> logger) : IVigilSettingsProvider
{
    public async ValueTask<VigilRuntimeSettings> GetAsync(CancellationToken ct = default)
    {
        try
        {
            var settings = await settingsService.GetValueAsync<VigilSettings>(SettingsKeys.Vigil, ct: ct);
            return settings is null ? Fallback() : Map(settings);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to read Vigil settings from Registry; using class defaults");
            return Fallback();
        }
    }

    private static VigilRuntimeSettings Fallback() => Map(new VigilSettings());

    private static VigilRuntimeSettings Map(VigilSettings dto)
    {
        return new()
        {
            PollIntervalSeconds = dto.PollIntervalSeconds,
            ProbeTimeoutSeconds = dto.ProbeTimeoutSeconds,
            DegradedLatencyMs = dto.DegradedLatencyMs,
            ConsecutiveFailuresToOpenIncident = dto.ConsecutiveFailuresToOpenIncident,
            SampleHistorySize = dto.SampleHistorySize,
            DeviceReportIntervalSeconds = dto.DeviceReportIntervalSeconds
        };
    }
}
