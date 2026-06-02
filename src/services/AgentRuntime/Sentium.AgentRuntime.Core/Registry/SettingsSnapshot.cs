namespace Sentium.AgentRuntime.Core.Registry;

/// <summary>
/// Consumer-side projection of the Registry's global settings response.
/// Property names must match the JSON emitted by Registry's SettingsDto.
/// </summary>
public sealed record SettingsSnapshot(HarnessSettingsSnapshot Harness)
{
    /// <summary>
    /// Fallback used when Registry is unreachable and no cached value exists.
    /// </summary>
    public static readonly SettingsSnapshot Default = new(
        Harness: new HarnessSettingsSnapshot(
            UserHarnessPrompt: string.Empty,
            IsBuiltInHarnessEnabled: true,
            IsPromptEnhancementEnabled: true)
    );
}

public sealed record HarnessSettingsSnapshot(
    string UserHarnessPrompt,
    bool IsBuiltInHarnessEnabled,
    bool IsPromptEnhancementEnabled = true);
