using Sentium.Registry.Contracts.Settings;

namespace Sentium.Praxis.Features.Registry;

/// <summary>
/// Praxis' immutable projection of the Registry <c>HarnessSettings</c> and <c>InferenceSettings</c> it consumes.
/// </summary>
internal sealed record SettingsSnapshot(HarnessSettingsSnapshot Harness, InferenceSettingsSnapshot? Inference = null)
{
    /// <summary>
    /// Fallback used when the settings cannot be read from Registry.
    /// </summary>
    public static readonly SettingsSnapshot Default = new(HarnessSettingsSnapshot.From(new HarnessSettings()), InferenceSettingsSnapshot.Default);
}
