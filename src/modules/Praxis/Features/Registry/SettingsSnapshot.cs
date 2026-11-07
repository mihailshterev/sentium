using Sentium.Core.Constants;

namespace Sentium.Praxis.Features.Registry;

/// <summary>
/// Praxis' projection of the Registry settings it consumes. Property names must match the JSON
/// properties of Registry's <c>HarnessSettings</c> and <c>InferenceSettings</c>, since
/// <c>ISettingsService.GetValueAsync</c> deserializes the stored JSON into these records.
/// </summary>
public sealed record SettingsSnapshot(HarnessSettingsSnapshot Harness, InferenceSettingsSnapshot? Inference = null)
{
    /// <summary>
    /// Fallback used when the settings cannot be read from Registry.
    /// </summary>
    public static readonly SettingsSnapshot Default = new(
        Harness: new HarnessSettingsSnapshot(
            UserHarnessPrompt: string.Empty,
            IsBuiltInHarnessEnabled: true,
            IsPromptEnhancementEnabled: false),
        Inference: InferenceSettingsSnapshot.Default
    );
}

public sealed record HarnessSettingsSnapshot(
    string UserHarnessPrompt,
    bool IsBuiltInHarnessEnabled,
    bool IsPromptEnhancementEnabled = false);

public sealed record InferenceSettingsSnapshot(
    string DefaultModel,
    float AgentTemperature)
{
    /// <summary>
    /// Mirrors the defaults of Registry's <c>InferenceSettings</c>.
    /// </summary>
    public static readonly InferenceSettingsSnapshot Default = new(
        DefaultModel: AIModels.Gemma4_E4B,
        AgentTemperature: 0.3f);
}
