namespace Sentium.Praxis.Features.Workflows;

internal sealed record WorkflowRunResponse(
    Guid Id,
    Guid? WorkflowId,
    string? Title,
    string TriggerType,
    string TriggerPayload,
    string Explanation,
    string Risk,
    string Recommendation,
    DateTime StartedAt,
    DateTime CompletedAt,
    IReadOnlyList<WorkflowLogEntry> Logs);
