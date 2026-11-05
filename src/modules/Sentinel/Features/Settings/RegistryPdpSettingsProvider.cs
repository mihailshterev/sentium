using Sentium.Core.Constants;
using Sentium.Registry.Contracts;

namespace Sentium.Sentinel.Features.Settings;

/// <summary>
/// Reads the runtime PDP settings from the Registry module in-process. Registry caches the settings
/// envelope itself (HybridCache L1 + L2) and evicts it on change, so Sentinel keeps no copy of its own.
/// </summary>
public sealed class RegistryPdpSettingsProvider(
    ISettingsService settingsService,
    ILogger<RegistryPdpSettingsProvider> logger) : IPdpRuntimeSettingsProvider
{
    public async ValueTask<PdpRuntimeSettings> GetAsync(CancellationToken ct = default)
    {
        try
        {
            var settings = await settingsService.GetValueAsync<PdpSettingsResponse>(SettingsKeys.Pdp, ct: ct);
            return settings is null ? Fallback() : Map(settings);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
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
