namespace Sentium.AgentRuntime.Core.Dtos;

public sealed record WorkflowAgentRef(
    Guid AgentId,
    int Order);

public sealed record WorkflowResponse(
    Guid Id,
    Guid UserId,
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
    string Scenario,
    Guid? WorkspaceId = null);

public sealed record WorkflowLogEntry(string Author, string Text, string Type);

public sealed record WorkflowRunResponse(
    Guid Id,
    Guid? WorkflowId,
    string TriggerType,
    string TriggerPayload,
    string Explanation,
    string Risk,
    string Recommendation,
    DateTime StartedAt,
    DateTime CompletedAt,
    IReadOnlyList<WorkflowLogEntry> Logs);
