using System.Linq.Expressions;
using Sentium.Praxis.Dtos;
using Sentium.Praxis.Entities;

namespace Sentium.Praxis.Features.Projections;

internal static class WorkflowRunProjections
{
    internal static WorkflowRunResponse ToResponse(WorkflowRun r) =>
        new(r.Id, r.WorkflowId, r.Title, r.TriggerType, r.TriggerPayload, r.Explanation, r.Risk, r.Recommendation, r.StartedAt, r.CompletedAt, r.Logs);

    internal static readonly Expression<Func<WorkflowRun, WorkflowRunSummaryResponse>> ToSummary =
        r => new WorkflowRunSummaryResponse(
            r.Id, r.WorkflowId, r.Title, r.TriggerType, r.TriggerPayload, r.Explanation, r.Risk, r.Recommendation, r.StartedAt, r.CompletedAt, r.Logs.Count);
}
