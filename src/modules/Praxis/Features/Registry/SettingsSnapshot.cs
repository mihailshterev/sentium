using Sentium.Core.Constants;

namespace Sentium.Praxis.Features.Registry;

/// <summary>
/// Praxis' projection of the Registry settings it consumes. Property names must match the JSON
/// properties of Registry's <c>HarnessSettings</c> and <c>OllamaSettings</c>, since
/// <c>ISettingsService.GetValueAsync</c> deserializes the stored JSON into these records.
/// </summary>
public sealed record SettingsSnapshot(HarnessSettingsSnapshot Harness, OllamaSettingsSnapshot? Ollama = null)
{
    /// <summary>
    /// Fallback used when the settings cannot be read from Registry.
    /// </summary>
    public static readonly SettingsSnapshot Default = new(
        Harness: new HarnessSettingsSnapshot(
            UserHarnessPrompt: string.Empty,
            IsBuiltInHarnessEnabled: true,
            IsPromptEnhancementEnabled: false),
        Ollama: new OllamaSettingsSnapshot(
            DefaultModel: AIModels.Gemma4_E4B,
            AgentTemperature: 0.3f,
            AgentContextWindow: 16384)
    );
}

public sealed record HarnessSettingsSnapshot(
    string UserHarnessPrompt,
    bool IsBuiltInHarnessEnabled,
    bool IsPromptEnhancementEnabled = false);

public sealed record OllamaSettingsSnapshot(
    string DefaultModel,
    float AgentTemperature,
    int AgentContextWindow);
