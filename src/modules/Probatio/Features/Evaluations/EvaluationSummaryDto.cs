using Sentium.Probatio.Entities;

namespace Sentium.Probatio.Features.Evaluations;

/// <summary>
/// An Evaluation without its results. <paramref name="JudgeModel"/> is <see langword="null"/> when the suite version
/// has no quality Checks.
/// </summary>
internal sealed record EvaluationSummaryDto(
    Guid Id,
    Guid SuiteId,
    string SuiteName,
    int SuiteVersion,
    string AgentName,
    Guid? BenchmarkId,
    string Model,
    string? JudgeModel,
    int Repetitions,
    EvaluationStatus Status,
    string? Error,
    int CaseCount,
    int CompletedCaseCount,
    int PassedCaseCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt);
