namespace Sentium.Praxis.Features.Workflows;

internal sealed record RunWorkflowRequest(
    Guid WorkflowId,
    string Scenario,
    Guid? WorkspaceId = null);
