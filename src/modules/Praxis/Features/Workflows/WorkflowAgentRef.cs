namespace Sentium.Praxis.Features.Workflows;

internal sealed record WorkflowAgentRef(
    Guid AgentId,
    int Order);
