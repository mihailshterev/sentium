using System.ComponentModel.DataAnnotations;
using Sentium.Core.Constants;

namespace Sentium.Registry.Contracts.Settings;

/// <summary>
/// Runtime-configurable Policy Decision Point settings, managed by Sovereign users and consumed
/// by the Sentinel module. Static policy lists (forbidden actions, protected prefixes) remain in
/// the <c>Pdp</c> configuration section (Sentinel's <c>PdpOptions</c>).
/// </summary>
[SettingsGroup(
    SettingsKeys.Pdp,
    SettingsScope.Household,
    "security",
    "Security",
    "How Sentinel's policy decision point judges every agent action.",
    Order = 2)]
public sealed class PdpSettings
{
    public const int MaxModelIdLength = 256;

    /// <summary>
    /// An emergency switch that lives on the Sentinel page, so it is not listed on the Settings page.
    /// </summary>
    public bool LockdownMode { get; set; } = false;

    [Setting(
        "Semantic intent check",
        "Asks a model to verify that each risky agent action (a write, code execution or external call) matches what the user asked for.",
        Keywords = ["sentinel", "pdp", "alignment", "llm"])]
    public bool SemanticIntentCheckEnabled { get; set; } = true;

    [Setting(
        "Intent check model",
        "The model that runs the semantic intent check. Empty uses the default model.",
        Keywords = ["sentinel", "pdp", "llm"],
        Control = SettingControlKind.Model,
        EmptyLabel = "Default model")]
    [MaxLength(MaxModelIdLength)]
    public string IntentCheckModel { get; set; } = string.Empty;

    [Setting(
        "Autonomy level",
        "Trades security for agent autonomy when the intent check is inconclusive.",
        Keywords = ["sentinel", "pdp", "security", "trust"],
        Control = SettingControlKind.Range,
        Step = 1,
        MinLabel = "High security",
        MaxLabel = "High autonomy")]
    [Range(1, 10)]
    public int AutonomyLevel { get; set; } = 5;

    [Setting(
        "Rate limit: requests",
        "Agent actions allowed per window before Sentinel denies further ones.",
        Keywords = ["sentinel", "pdp", "throttle", "quota"],
        Unit = "requests")]
    [Range(1, int.MaxValue)]
    public int RateLimitMaxRequests { get; set; } = 120;

    [Setting(
        "Rate limit: window",
        "The window the request limit is counted over.",
        Keywords = ["sentinel", "pdp", "throttle", "quota"],
        Unit = "s")]
    [Range(1, int.MaxValue)]
    public int RateLimitWindowSeconds { get; set; } = 60;
}
