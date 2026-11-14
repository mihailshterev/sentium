using System.Text.Json.Serialization;

namespace Sentium.Praxis.Features.Workflows;

public sealed record WorkflowTriggerPayload(
    [property: JsonPropertyName("activity")] string Activity,
    [property: JsonPropertyName("workflowId")] Guid WorkflowId,
    [property: JsonPropertyName("workflowName")] string WorkflowName,
    [property: JsonPropertyName("agents")] IReadOnlyList<Guid> Agents,
    [property: JsonPropertyName("workspaceId")] Guid? WorkspaceId,
    [property: JsonPropertyName("userId")] Guid? UserId,
    [property: JsonPropertyName("streamId")] string? StreamId = null);
