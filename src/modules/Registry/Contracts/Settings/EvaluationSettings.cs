namespace Sentium.Registry.Contracts.Settings;

/// <summary>
/// A user's own Evaluation preferences.
/// </summary>
public sealed class EvaluationSettings
{
    public const int MaxModelIdLength = 256;

    /// <summary>
    /// The Judge that scores quality Checks; <see langword="null"/> uses the default model. Each Evaluation freezes
    /// the Judge it started with.
    /// </summary>
    public string? JudgeModel { get; set; }
}
