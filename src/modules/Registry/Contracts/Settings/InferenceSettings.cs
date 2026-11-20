using System.ComponentModel.DataAnnotations;
using Sentium.Core.Constants;

namespace Sentium.Registry.Contracts.Settings;

/// <summary>
/// Global agent inference settings. Server endpoints and per-model context sizes are deployment
/// configuration (the <c>Inference</c> section and the server's model presets), not runtime settings.
/// </summary>
[SettingsGroup(
    SettingsKeys.Inference,
    SettingsScope.Household,
    "inference",
    "Inference",
    "Model and sampling parameters for every agent; they apply from the next agent call. Server endpoints and per-model context size are deployment configuration.",
    Order = 0)]
public sealed class InferenceSettings
{
    public const int MaxModelIdLength = 256;

    [Setting(
        "Default model",
        "The chat model agents use unless they pick their own.",
        Keywords = ["llm", "model", "llama"],
        Control = SettingControlKind.Model)]
    [Required]
    [MaxLength(MaxModelIdLength)]
    public string DefaultModel { get; set; } = AIModels.Gemma4_E4B;

    [Setting(
        "Temperature",
        "Lower is more precise and repeatable, higher is more creative.",
        Keywords = ["sampling", "creativity", "randomness"],
        Step = 0.05,
        MinLabel = "Precise (0.0)",
        MaxLabel = "Creative (1.0)")]
    [Range(0.0, 1.0)]
    public float AgentTemperature { get; set; } = 0.3f;
}
