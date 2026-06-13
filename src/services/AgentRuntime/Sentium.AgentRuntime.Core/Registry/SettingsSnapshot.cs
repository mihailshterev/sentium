using Sentium.Shared.Constants;

namespace Sentium.AgentRuntime.Core.Registry;

/// <summary>
/// Consumer-side projection of the Registry's global settings response.
/// Property names must match the JSON emitted by Registry's SettingsDto.
/// </summary>
public sealed record SettingsSnapshot(HarnessSettingsSnapshot Harness, OllamaSettingsSnapshot? Ollama = null)
{
    /// <summary>
    /// Fallback used when Registry is unreachable and no cached value exists.
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

public sealed record SettingsEnvelope<T>(string Key, T Value, DateTimeOffset UpdatedAt, string? UpdatedBy);
