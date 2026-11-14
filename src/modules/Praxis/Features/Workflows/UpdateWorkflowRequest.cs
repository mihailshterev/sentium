namespace Sentium.Praxis.Features.Workflows;

public sealed record UpdateWorkflowRequest(
    string Name,
    string Description,
    IReadOnlyList<WorkflowAgentRef> Agents);
