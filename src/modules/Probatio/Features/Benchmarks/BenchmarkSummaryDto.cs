using Sentium.Probatio.Features.Evaluations;

namespace Sentium.Probatio.Features.Benchmarks;

internal sealed record BenchmarkSummaryDto(
    Guid Id,
    Guid SuiteId,
    string SuiteName,
    int SuiteVersion,
    string AgentName,
    int Repetitions,
    string? JudgeModel,
    DateTimeOffset CreatedAt,
    IReadOnlyList<EvaluationSummaryDto> Evaluations);
