using AgentRuntime.Core.Workflows;

namespace AgentRuntime.Core.Orchestration;

public interface IOrchestrator
{
    Task<WorkflowResult> RunAsync(WorkflowTrigger trigger, CancellationToken ct = default);
}
