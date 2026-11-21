namespace Sentium.Sentinel.Features.Settings;

/// <summary>
/// Provides cached, Registry-backed access to the runtime PDP settings.
/// </summary>
internal interface IPdpRuntimeSettingsProvider
{
    ValueTask<PdpRuntimeSettings> GetAsync(CancellationToken ct = default);
}
