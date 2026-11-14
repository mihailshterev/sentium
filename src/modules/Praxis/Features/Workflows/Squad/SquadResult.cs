namespace Sentium.Praxis.Features.Workflows.Squad;

/// <summary>
/// The outcome of a squad run.
/// </summary>
public sealed record SquadResult(string Answer, RiskLevel Risk, string Recommendation, IReadOnlyList<StepResult> Steps);
