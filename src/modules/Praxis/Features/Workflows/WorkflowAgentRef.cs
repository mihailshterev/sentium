namespace Sentium.Praxis.Features.Workflows;

public sealed record WorkflowAgentRef(
    Guid AgentId,
    int Order);
