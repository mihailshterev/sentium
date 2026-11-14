namespace Sentium.Praxis.Features.Workflows.Squad;

/// <summary>
/// The outcome of a squad run.
/// </summary>
internal sealed record SquadResult(string Answer, RiskLevel Risk, string Recommendation, IReadOnlyList<StepResult> Steps);
