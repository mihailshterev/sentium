using System.Text.Json.Serialization;

namespace Sentium.AgentRuntime.Core.Dtos;

public sealed record WorkflowAcceptedResponse(string EventId, string? CorrelationId = null, string Status = "Accepted");

public sealed record WorkflowTriggerPayload(
    [property: JsonPropertyName("activity")] string Activity,
    [property: JsonPropertyName("workflowId")] Guid WorkflowId,
    [property: JsonPropertyName("workflowName")] string WorkflowName,
    [property: JsonPropertyName("agents")] IReadOnlyList<Guid> Agents,
    [property: JsonPropertyName("workspaceId")] Guid? WorkspaceId,
    [property: JsonPropertyName("userId")] Guid? UserId,
    [property: JsonPropertyName("streamId")] string? StreamId = null);

public sealed record DynamicWorkflowActivity(
    [property: JsonPropertyName("activity")] string Activity,
    [property: JsonPropertyName("user")] string User);
