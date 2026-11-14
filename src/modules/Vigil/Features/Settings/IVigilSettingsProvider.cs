namespace Sentium.Vigil.Features.Settings;

/// <summary>
/// Supplies the current vigil runtime settings (poll interval, thresholds, history size).
/// </summary>
internal interface IVigilSettingsProvider
{
    ValueTask<VigilRuntimeSettings> GetAsync(CancellationToken ct = default);
}
