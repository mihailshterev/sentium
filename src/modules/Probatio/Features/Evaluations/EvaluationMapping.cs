using System.Linq.Expressions;
using Sentium.Probatio.Entities;

namespace Sentium.Probatio.Features.Evaluations;

internal static class EvaluationMapping
{
    public static readonly Expression<Func<Evaluation, EvaluationSummaryDto>> ToSummary = e => new EvaluationSummaryDto(
        e.Id,
        e.SuiteId,
        e.SuiteName,
        e.SuiteVersion,
        e.AgentName,
        e.BenchmarkId,
        e.Model,
        e.JudgeModel,
        e.Repetitions,
        e.Status,
        e.Error,
        e.CaseCount,
        e.CompletedCaseCount,
        e.PassedCaseCount,
        e.CreatedAt,
        e.StartedAt,
        e.FinishedAt);

    public static readonly Func<Evaluation, EvaluationSummaryDto> ToSummaryDto = ToSummary.Compile();

    /// <param name="servedModels">The models the catalog serves now; <see langword="null"/> when it is unavailable.</param>
    public static EvaluationDto ToDto(Evaluation evaluation, IEnumerable<EvaluationCaseResult> results, IReadOnlySet<string>? servedModels)
    {
        ArgumentNullException.ThrowIfNull(evaluation);

        var byCase = results.ToDictionary(r => r.CaseId);
        var cases = evaluation.Cases.Select((c, position) =>
        {
            var result = byCase.GetValueOrDefault(c.Id);
            return new EvaluationCaseDto(
                c.Id,
                position,
                c.Name,
                c.Prompt,
                [.. c.Checks.Select(check => check.Type)],
                result?.Passed,
                result?.PassedRepetitions ?? 0,
                result?.Repetitions ?? []);
        });

        return new EvaluationDto(ToSummaryDto(evaluation), [.. cases], servedModels is null ? null : !servedModels.Contains(evaluation.Model));
    }
}
