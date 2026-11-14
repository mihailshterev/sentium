namespace Sentium.Praxis.Features.Workflows;

internal sealed record UpdateWorkflowRequest(
    string Name,
    string Description,
    IReadOnlyList<WorkflowAgentRef> Agents);
