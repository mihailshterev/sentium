using Sentium.AgentRuntime.Core.Dtos;
using Sentium.AgentRuntime.Core.Entities;

namespace Sentium.AgentRuntime.Infrastructure.Projections;

internal static class WorkflowRunProjections
{
    internal static WorkflowRunResponse ToResponse(WorkflowRun r) =>
        new(r.Id, r.TriggerType, r.TriggerPayload, r.Explanation, r.Risk, r.Recommendation, r.StartedAt, r.CompletedAt, r.Logs);
}
