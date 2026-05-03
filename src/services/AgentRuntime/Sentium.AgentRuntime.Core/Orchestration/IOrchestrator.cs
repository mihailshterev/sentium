using Sentium.AgentRuntime.Core.Workflows;

namespace Sentium.AgentRuntime.Core.Orchestration;

public interface IOrchestrator
{
    Task<WorkflowResult> RunAsync(WorkflowTrigger trigger, CancellationToken ct = default);
}
