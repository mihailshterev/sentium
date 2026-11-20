using System.ComponentModel.DataAnnotations;
using Sentium.Core.Constants;

namespace Sentium.Registry.Contracts.Settings;

/// <summary>
/// Controls the global system-prompt harness prepended to every agent inference call.
/// </summary>
[SettingsGroup(
    SettingsKeys.Harness,
    SettingsScope.Personal,
    "agents",
    "Agents",
    "How every agent you run is instructed.",
    Order = 0)]
public sealed class HarnessSettings
{
    public const int MaxUserHarnessPromptLength = 16_000;

    /// <summary>
    /// When true (default) the built-in <c>UniversalSystemHarness.Policy</c> is prepended.
    /// Set to false to rely solely on <see cref="UserHarnessPrompt"/>.
    /// </summary>
    [Setting(
        "Built-in governance harness",
        "Prepends the Universal Agent Governance policy (chain-of-thought, retrieval-first, anti-hallucination) to every agent interaction. Recommended to keep on.",
        Keywords = ["policy", "safety", "hallucination", "system prompt"])]
    public bool IsBuiltInHarnessEnabled { get; set; } = true;

    /// <summary>
    /// When true the user's prompt is rewritten by a fast pre-execution enhancement pass
    /// before the agent runs, to improve results from smaller local models.
    /// </summary>
    [Setting(
        "Prompt enhancement",
        "Rewrites assistant and workflow prompts for clarity and specificity before the agent runs, for sharper results from smaller local models. The original is kept and the enhanced version is shown inline.",
        Keywords = ["rewrite", "optimize", "pre-pass"])]
    public bool IsPromptEnhancementEnabled { get; set; } = false;

    /// <summary>
    /// User-authored text injected into every agent alongside the built-in policy.
    /// Empty string means no injection.
    /// </summary>
    [Setting(
        "Global prompt",
        "Injected into every agent alongside the built-in harness: personas, tone, domain focus or operational constraints. Plain text or markdown.",
        Keywords = ["persona", "tone", "instructions", "behavior", "system prompt"],
        Control = SettingControlKind.Textarea,
        Rows = 8,
        Placeholder = "Examples:\n- \"Always respond in a professional, concise tone.\"\n- \"You are a local AI orchestrator. Prioritize efficient workflow execution.\"")]
    [MaxLength(MaxUserHarnessPromptLength)]
    public string UserHarnessPrompt { get; set; } = string.Empty;
}
