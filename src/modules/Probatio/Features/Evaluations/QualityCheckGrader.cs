using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Quality;
using Sentium.Probatio.Common;
using Sentium.Probatio.Entities;
using EvalCheck = Sentium.Probatio.Entities.EvalCheck;

namespace Sentium.Probatio.Features.Evaluations;

/// <summary>
/// Scores quality Checks 1-5 with the Microsoft.Extensions.AI.Evaluation quality evaluators, asking the Judge
/// through <paramref name="judge"/>. A Check passes at its threshold (default 4); a Judge that gives no usable
/// score fails the Check rather than the Evaluation.
/// </summary>
internal sealed class QualityCheckGrader(ChatConfiguration judge)
{
    public async Task<CheckOutcome> GradeAsync(
        EvalCheck check,
        EvalCase evalCase,
        IReadOnlyList<ChatMessage> request,
        ChatResponse response,
        CancellationToken ct)
    {
        var threshold = check.Threshold ?? ProbatioLimits.DefaultQualityThreshold;
        var (evaluator, metricName, context) = Evaluator(check.Type, evalCase);

        try
        {
            var result = await evaluator.EvaluateAsync(WithHistory(request), response, judge, context, ct);
            var metric = result.Get<NumericMetric>(metricName);

            if (metric.Value is not { } score)
            {
                var problem = metric.Diagnostics?.FirstOrDefault()?.Message ?? "no score in its reply";
                return new CheckOutcome(check.Type, CheckOutcomeStatus.Failed, ErrorText.Fit($"The Judge gave no usable score: {problem}"), null, threshold);
            }

            var status = score >= threshold ? CheckOutcomeStatus.Passed : CheckOutcomeStatus.Failed;
            return new CheckOutcome(check.Type, status, metric.Reason is null ? null : ErrorText.Fit(metric.Reason), score, threshold);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            return new CheckOutcome(check.Type, CheckOutcomeStatus.Failed, ErrorText.Fit($"The Judge could not score the answer: {ex.Message}"), null, threshold);
        }
    }

    private static (IEvaluator Evaluator, string MetricName, EvaluationContext[] Context) Evaluator(EvalCheckType type, EvalCase evalCase) => type switch
    {
        EvalCheckType.Relevance => (new RelevanceEvaluator(), RelevanceEvaluator.RelevanceMetricName, []),
        EvalCheckType.Coherence => (new CoherenceEvaluator(), CoherenceEvaluator.CoherenceMetricName, []),
        EvalCheckType.Groundedness => (new GroundednessEvaluator(), GroundednessEvaluator.GroundednessMetricName, [new GroundednessEvaluatorContext(GroundingContext(evalCase))]),
        EvalCheckType.Completeness => (new CompletenessEvaluator(), CompletenessEvaluator.CompletenessMetricName, [new CompletenessEvaluatorContext(evalCase.ExpectedAnswer ?? string.Empty)]),
        _ => throw new InvalidOperationException($"{type} is not a quality Check."),
    };

    /// <summary>
    /// The quality evaluators show the Judge only the last user message, so a follow-up such as "So what should I
    /// do?" reads as a question without context. Earlier turns are folded into that message instead.
    /// </summary>
    private static IReadOnlyList<ChatMessage> WithHistory(IReadOnlyList<ChatMessage> request)
    {
        if (request.Count <= 1)
        {
            return request;
        }

        var history = string.Join("\n", request.Take(request.Count - 1).Select(m => $"{(m.Role == ChatRole.User ? "User" : "Assistant")}: {m.Text}"));
        return [new ChatMessage(ChatRole.User, $"Earlier in this conversation:\n{history}\n\nThe user now asks: {request[^1].Text}")];
    }

    private static string GroundingContext(EvalCase evalCase)
        => string.Join("\n\n", evalCase.CannedToolResults.Select(r => $"Result of {r.ToolName}:\n{r.Result}"));
}
