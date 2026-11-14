namespace Sentium.Sentinel.Features.Settings;

/// <summary>
/// Provides cached, Registry-backed access to the runtime PDP settings.
/// </summary>
internal interface IPdpRuntimeSettingsProvider
{
    /// <summary>
    /// Returns the current runtime PDP settings.
    /// </summary>
    ValueTask<PdpRuntimeSettings> GetAsync(CancellationToken ct = default);
}
