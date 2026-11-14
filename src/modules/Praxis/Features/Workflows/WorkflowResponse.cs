namespace Sentium.Praxis.Features.Workflows;

public sealed record WorkflowResponse(
    Guid Id,
    Guid UserId,
    string Name,
    string Description,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<WorkflowAgentRef> Agents);
