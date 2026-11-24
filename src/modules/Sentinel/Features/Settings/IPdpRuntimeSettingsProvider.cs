namespace Sentium.Sentinel.Features.Settings;

/// <summary>
/// The runtime PDP settings, read from the Registry through its cache.
/// </summary>
internal interface IPdpRuntimeSettingsProvider
{
    ValueTask<PdpRuntimeSettings> GetAsync(CancellationToken ct = default);
}
