namespace Sentium.Praxis.Features.Workflows;

public sealed record CreateWorkflowRequest(
    string Name,
    string Description,
    IReadOnlyList<WorkflowAgentRef> Agents);
