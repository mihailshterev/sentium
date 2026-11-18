namespace Sentium.Probatio.Entities;

/// <summary>
/// One Check of an Eval Case. <paramref name="Threshold"/> is the passing Judge score (1-5) of a quality Check;
/// <see langword="null"/> uses the default, and deterministic Checks never have one.
/// </summary>
internal sealed record EvalCheck(EvalCheckType Type, int? Threshold = null);
