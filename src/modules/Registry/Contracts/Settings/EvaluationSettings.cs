using System.ComponentModel.DataAnnotations;
using Sentium.Core.Constants;

namespace Sentium.Registry.Contracts.Settings;

/// <summary>
/// A user's own Evaluation preferences.
/// </summary>
[SettingsGroup(
    SettingsKeys.Evaluation,
    SettingsScope.Personal,
    "evaluations",
    "Evaluations",
    "How your Evaluations are graded. Each Evaluation keeps the Judge it started with, so changing this only affects new ones.",
    Order = 3)]
public sealed class EvaluationSettings
{
    public const int MaxModelIdLength = 256;

    /// <summary>
    /// The Judge that scores quality Checks; <see langword="null"/> uses the default model. Each Evaluation freezes
    /// the Judge it started with.
    /// </summary>
    [Setting(
        "Judge model",
        "The model that scores quality Checks (relevance, coherence, groundedness, completeness). Pick a different model from the one you evaluate: a model tends to rate its own answers too kindly.",
        Keywords = ["judge", "grader", "evaluation", "probatio", "quality", "llm"],
        Control = SettingControlKind.Model,
        EmptyLabel = "Default model")]
    [MaxLength(MaxModelIdLength)]
    public string? JudgeModel { get; set; }
}
