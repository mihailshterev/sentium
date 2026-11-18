using Sentium.Probatio.Features.Evaluations;

namespace Sentium.Probatio.Features.Benchmarks;

/// <summary>
/// A Benchmark with the full results of its Evaluations, ordered by model, for the side-by-side comparison.
/// </summary>
internal sealed record BenchmarkDto(
    Guid Id,
    Guid SuiteId,
    string SuiteName,
    int SuiteVersion,
    string AgentName,
    int Repetitions,
    string? JudgeModel,
    DateTimeOffset CreatedAt,
    IReadOnlyList<EvaluationDto> Evaluations);
