namespace Sentium.Praxis.Features.Workflows;

internal sealed record CreateWorkflowRequest(
    string Name,
    string Description,
    IReadOnlyList<WorkflowAgentRef> Agents);
