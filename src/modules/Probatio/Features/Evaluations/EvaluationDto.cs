namespace Sentium.Probatio.Features.Evaluations;

/// <summary>
/// An Evaluation with its cases and results. <paramref name="ModelDeleted"/> is whether the evaluated model is no
/// longer served; <see langword="null"/> when the model catalog could not be asked.
/// </summary>
internal sealed record EvaluationDto(EvaluationSummaryDto Summary, IReadOnlyList<EvaluationCaseDto> Cases, bool? ModelDeleted);
