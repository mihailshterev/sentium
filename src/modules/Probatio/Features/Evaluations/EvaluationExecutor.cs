using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Sentium.Praxis.Contracts;
using Sentium.Probatio.Common;
using Sentium.Probatio.Data;
using Sentium.Probatio.Entities;

namespace Sentium.Probatio.Features.Evaluations;

/// <summary>
/// Executes one claimed Evaluation from its frozen cases and agent, saving each case's result as soon as it is
/// graded so progress is visible while it works. Status changes belong to <see cref="EvaluationWorker"/>.
/// </summary>
internal sealed class EvaluationExecutor(
    ProbatioDbContext db,
    IEvaluationAgentBuilder agents,
    ILogger<EvaluationExecutor> logger)
{
    private const string UserRole = "user";
    private const string AssistantRole = "assistant";
    private const string NoJudgeReason = "No Judge was chosen when this Evaluation started.";

    public async Task ExecuteAsync(Guid evaluationId, CancellationToken ct)
    {
        var evaluation = await db.Evaluations.FirstAsync(e => e.Id == evaluationId, ct);
        var judge = evaluation.JudgeModel is { } judgeModel
            ? new QualityCheckGrader(new ChatConfiguration(agents.CreateJudgeClient(judgeModel)))
            : null;

        for (var position = 0; position < evaluation.Cases.Count; position++)
        {
            var result = await EvaluateCaseAsync(evaluation, judge, evaluation.Cases[position], position, ct);

            db.EvaluationCaseResults.Add(result);
            evaluation.CompletedCaseCount++;
            if (result.Passed)
            {
                evaluation.PassedCaseCount++;
            }

            await db.SaveChangesAsync(CancellationToken.None);
        }
    }

    private async Task<EvaluationCaseResult> EvaluateCaseAsync(
        Evaluation evaluation,
        QualityCheckGrader? judge,
        EvalCase evalCase,
        int position,
        CancellationToken ct)
    {
        var repetitions = new List<EvaluationRepetition>(evaluation.Repetitions);
        for (var number = 1; number <= evaluation.Repetitions; number++)
        {
            repetitions.Add(await RepeatAsync(evaluation.Agent, judge, evalCase, number, ct));
        }

        var passed = repetitions.Count(r => r.Passed);

        return new EvaluationCaseResult
        {
            Id = Guid.CreateVersion7(),
            UserId = evaluation.UserId,
            EvaluationId = evaluation.Id,
            CaseId = evalCase.Id,
            Position = position,
            Passed = passed == repetitions.Count,
            PassedRepetitions = passed,
            Repetitions = repetitions,
        };
    }

    private async Task<EvaluationRepetition> RepeatAsync(
        EvaluationAgentSnapshot snapshot,
        QualityCheckGrader? judge,
        EvalCase evalCase,
        int number,
        CancellationToken ct)
    {
        var stub = new CannedToolStub(evalCase.CannedToolResults);
        var messages = ToMessages(evalCase);
        var transcript = evalCase.PriorTurns
            .Select(t => new TranscriptMessage(t.Role == EvalTurnRole.User ? UserRole : AssistantRole, t.Text))
            .Append(new TranscriptMessage(UserRole, evalCase.Prompt))
            .ToList();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var agent = await agents.CreateAsync(snapshot, stub.AnswerAsync, ct);
            var response = await agent.RunAsync(messages, cancellationToken: ct);
            stopwatch.Stop();

            transcript.AddRange(response.Messages
                .Where(m => m.Role == ChatRole.Assistant && !string.IsNullOrWhiteSpace(m.Text))
                .Select(m => new TranscriptMessage(AssistantRole, m.Text)));

            var checks = await GradeAsync(judge, evalCase, messages, response.Messages, response.Text, ct);
            var outputTokens = response.Usage?.OutputTokenCount;

            return new EvaluationRepetition(
                number,
                checks.All(c => c.Status != CheckOutcomeStatus.Failed),
                transcript,
                stub.Calls,
                checks,
                stopwatch.ElapsedMilliseconds,
                response.Usage?.InputTokenCount,
                outputTokens,
                TokensPerSecond(outputTokens, stopwatch.Elapsed),
                null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            stopwatch.Stop();
            logger.LogWarning(ex, "Eval Case {CaseId} repetition {Number} got no answer from the agent.", evalCase.Id, number);

            return new EvaluationRepetition(
                number,
                false,
                transcript,
                stub.Calls,
                [.. evalCase.Checks.Select(c => new CheckOutcome(c.Type, CheckOutcomeStatus.Skipped, "The agent did not answer."))],
                stopwatch.ElapsedMilliseconds,
                null,
                null,
                null,
                ErrorText.Fit(ex.Message));
        }
    }

    private static async Task<List<CheckOutcome>> GradeAsync(
        QualityCheckGrader? judge,
        EvalCase evalCase,
        List<ChatMessage> request,
        IList<ChatMessage> answer,
        string answerText,
        CancellationToken ct)
    {
        var item = EvalCheckGrader.ToItem(evalCase, answerText, [.. request, .. answer]);
        var outcomes = new List<CheckOutcome>(evalCase.Checks.Count);

        foreach (var check in evalCase.Checks)
        {
            if (!EvalCheckRules.IsQuality(check.Type))
            {
                outcomes.Add(EvalCheckGrader.Grade(check, evalCase, item));
            }
            else if (judge is null)
            {
                outcomes.Add(new CheckOutcome(check.Type, CheckOutcomeStatus.Failed, NoJudgeReason));
            }
            else
            {
                outcomes.Add(await judge.GradeAsync(check, evalCase, request, new ChatResponse([.. answer]), ct));
            }
        }

        return outcomes;
    }

    private static List<ChatMessage> ToMessages(EvalCase evalCase)
    {
        return
        [
            .. evalCase.PriorTurns.Select(t => new ChatMessage(t.Role == EvalTurnRole.User ? ChatRole.User : ChatRole.Assistant, t.Text)),
            new ChatMessage(ChatRole.User, evalCase.Prompt),
        ];
    }

    private static double? TokensPerSecond(long? outputTokens, TimeSpan elapsed)
        => outputTokens is { } tokens && elapsed.TotalSeconds > 0 ? Math.Round(tokens / elapsed.TotalSeconds, 2) : null;
}
