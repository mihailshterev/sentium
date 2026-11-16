namespace Sentium.Praxis.Features.Workflows;

/// <summary>
/// The stored JSON shape of a <see cref="WorkflowTrigger"/>.
/// </summary>
internal sealed record WorkflowTriggerPayload(string Activity, Guid? WorkflowId, string? WorkflowName, Guid? WorkspaceId);
