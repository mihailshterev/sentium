namespace AgentRuntime.Core.Workflows;

public interface IAgentWorkflow
{
    WorkflowType Type { get; }
    Task<WorkflowResult> ExecuteAsync(WorkflowTrigger trigger, CancellationToken ct);
}
