namespace AgentRuntime.Core.Dtos;

public sealed record WorkflowAgentRef(
    Guid AgentId,
    int Order);

public sealed record WorkflowResponse(
    Guid Id,
    string Name,
    string Description,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<WorkflowAgentRef> Agents);

public sealed record CreateWorkflowRequest(
    string Name,
    string Description,
    IReadOnlyList<WorkflowAgentRef> Agents);

public sealed record UpdateWorkflowRequest(
    string Name,
    string Description,
    IReadOnlyList<WorkflowAgentRef> Agents);

public sealed record RunWorkflowRequest(
    Guid WorkflowId,
    string Scenario);
