namespace Sentium.Probatio.Features.Benchmarks;

/// <summary>
/// Evaluates <paramref name="AgentName"/> against one suite version (<see langword="null"/>: the current one) on
/// each of <paramref name="Models"/>.
/// </summary>
internal sealed record StartBenchmarkRequest(Guid SuiteId, int? SuiteVersion, string AgentName, IReadOnlyList<string> Models, int Repetitions = 1);
