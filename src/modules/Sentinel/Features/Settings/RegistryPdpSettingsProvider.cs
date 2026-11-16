using Sentium.Core.Constants;
using Sentium.Registry.Contracts;
using Sentium.Registry.Contracts.Settings;

namespace Sentium.Sentinel.Features.Settings;

/// <summary>
/// Reads the runtime PDP settings from the Registry module in-process. Registry caches the settings
/// envelope itself (HybridCache) and evicts it on change, so Sentinel keeps no copy of its own.
/// </summary>
internal sealed class RegistryPdpSettingsProvider(
    ISettingsService settingsService,
    ILogger<RegistryPdpSettingsProvider> logger) : IPdpRuntimeSettingsProvider
{
    public async ValueTask<PdpRuntimeSettings> GetAsync(CancellationToken ct = default)
    {
        try
        {
            var settings = await settingsService.GetValueAsync<PdpSettings>(SettingsKeys.Pdp, ct: ct);
            return settings is null ? Fallback() : Map(settings);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The kill switch must not turn itself off because its setting cannot be read: fail closed.
            logger.LogError(ex, "Failed to read PDP settings from Registry; locking down until they can be read again");
            return Fallback() with { LockdownMode = true };
        }
    }

    private static PdpRuntimeSettings Fallback() => Map(new PdpSettings());

    private static PdpRuntimeSettings Map(PdpSettings dto)
    {
        return new()
        {
            LockdownMode = dto.LockdownMode,
            AutonomyLevel = dto.AutonomyLevel,
            SemanticIntentCheckEnabled = dto.SemanticIntentCheckEnabled,
            IntentCheckModel = dto.IntentCheckModel,
            RateLimitMaxRequests = dto.RateLimitMaxRequests,
            RateLimitWindowSeconds = dto.RateLimitWindowSeconds
        };
    }
}
