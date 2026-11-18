namespace Sentium.Probatio.Entities;

/// <summary>
/// The outcome of one Check in one repetition. For a quality Check, <paramref name="Score"/> is the Judge's 1-5
/// score (<see langword="null"/> when it gave none), <paramref name="Threshold"/> the score it needed, and
/// <paramref name="Reason"/> the Judge's reasoning.
/// </summary>
internal sealed record CheckOutcome(EvalCheckType Type, CheckOutcomeStatus Status, string? Reason, double? Score = null, int? Threshold = null);
