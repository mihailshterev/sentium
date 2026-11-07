using Sentium.Praxis.Features.Orchestration;

namespace Sentium.Praxis.Features.Workflows.Squad;

internal static class SquadResultMapper
{
    public static WorkflowResult ToWorkflowResult(SquadResult result, StreamLogAccumulator log, string emptyExplanation, Guid? workflowId, Guid? userId) => new()
    {
        Explanation = string.IsNullOrWhiteSpace(result.Answer) ? emptyExplanation : result.Answer,
        Risk = result.Risk.ToString(),
        Recommendation = result.Recommendation,
        History = [.. result.Steps.Select(s => ($"{s.Step.Agent}{(s.Verified ? string.Empty : " (unverified)")}", s.Step.Task))],
        StreamLog = log.Entries,
        WorkflowId = workflowId,
        UserId = userId,
    };
}
