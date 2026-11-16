namespace Sentium.Praxis.Features.Orchestration;

internal sealed record RunDynamicWorkflowRequest(string Activity, Guid? WorkspaceId = null);
