namespace Sentium.Praxis.Contracts;

/// <summary>
/// One of the user's own (instructions-only) skills, frozen as it was when an Evaluation started.
/// </summary>
public sealed record EvaluationSkill(string Name, string Description, string Instructions);
