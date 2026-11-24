namespace Sentium.Praxis.Features.Workflows;

/// <summary>
/// An executable agent workflow keyed by its <see cref="WorkflowType"/>.
/// </summary>
internal interface IAgentWorkflow
{
    WorkflowType Type { get; }

    Task<WorkflowResult> ExecuteAsync(WorkflowTrigger trigger, CancellationToken ct);
}
