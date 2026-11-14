namespace Sentium.Registry.Features.Settings;

/// <summary>
/// Controls the global system-prompt harness prepended to every agent inference call.
/// </summary>
internal sealed class HarnessSettings
{
    /// <summary>
    /// User-authored text injected into every agent alongside the built-in policy.
    /// Empty string means no injection.
    /// </summary>
    public string UserHarnessPrompt { get; set; } = string.Empty;

    /// <summary>
    /// When true (default) the built-in <c>UniversalSystemHarness.Policy</c> is prepended.
    /// Set to false to rely solely on <see cref="UserHarnessPrompt"/>.
    /// </summary>
    public bool IsBuiltInHarnessEnabled { get; set; } = true;

    /// <summary>
    /// When true the user's prompt is rewritten by a fast pre-execution enhancement pass
    /// before the agent runs, to improve results from smaller local models.
    /// </summary>
    public bool IsPromptEnhancementEnabled { get; set; } = false;
}
