namespace Sentium.Praxis.Features.Registry;

public sealed record HarnessSettingsSnapshot(
    string UserHarnessPrompt,
    bool IsBuiltInHarnessEnabled,
    bool IsPromptEnhancementEnabled = false);
