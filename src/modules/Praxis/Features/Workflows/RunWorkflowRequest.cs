namespace Sentium.Praxis.Features.Workflows;

public sealed record RunWorkflowRequest(
    Guid WorkflowId,
    string Scenario,
    Guid? WorkspaceId = null);
