namespace Sentium.AgentRuntime.Core.Workflows;

/// <summary>
/// An executable agent workflow keyed by its <see cref="WorkflowType"/>.
/// </summary>
public interface IAgentWorkflow
{
    WorkflowType Type { get; }

    /// <summary>
    /// Runs the workflow for the given trigger and returns its result.
    /// </summary>
    Task<WorkflowResult> ExecuteAsync(WorkflowTrigger trigger, CancellationToken ct);
}
