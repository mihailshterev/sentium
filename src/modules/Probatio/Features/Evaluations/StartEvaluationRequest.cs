namespace Sentium.Probatio.Features.Evaluations;

/// <summary>
/// Evaluates <paramref name="AgentName"/> on <paramref name="Model"/> (<see langword="null"/>: the default model)
/// against a suite version (<see langword="null"/>: the current one).
/// </summary>
internal sealed record StartEvaluationRequest(Guid SuiteId, int? SuiteVersion, string AgentName, string? Model, int Repetitions = 1);
