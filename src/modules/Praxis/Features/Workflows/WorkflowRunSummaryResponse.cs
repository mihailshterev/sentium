namespace Sentium.Praxis.Features.Workflows;

public sealed record WorkflowRunSummaryResponse(
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
    int LogCount);
