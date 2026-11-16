using Sentium.Registry.Contracts.Settings;

namespace Sentium.Praxis.Features.Registry;

internal sealed record HarnessSettingsSnapshot(
    string UserHarnessPrompt,
    bool IsBuiltInHarnessEnabled,
    bool IsPromptEnhancementEnabled = false)
{
    public static HarnessSettingsSnapshot From(HarnessSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new(settings.UserHarnessPrompt, settings.IsBuiltInHarnessEnabled, settings.IsPromptEnhancementEnabled);
    }
}
