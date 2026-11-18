using Sentium.Probatio.Entities;

namespace Sentium.Probatio.Features.Evaluations;

/// <summary>
/// One frozen case with its result; <paramref name="Passed"/> is <see langword="null"/> until the case is graded.
/// </summary>
internal sealed record EvaluationCaseDto(
    Guid CaseId,
    int Position,
    string? Name,
    string Prompt,
    IReadOnlyList<EvalCheckType> Checks,
    bool? Passed,
    int PassedRepetitions,
    IReadOnlyList<EvaluationRepetition> Repetitions);
