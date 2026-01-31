using AgentRuntime.Core.Workflows;

namespace AgentRuntime.Application.Workflows;

public sealed class NetworkAnalysisWorkflow : IAgentWorkflow
{
    public WorkflowType Type => WorkflowType.Predefined;

    public Task<WorkflowResult> ExecuteAsync(WorkflowTrigger trigger, CancellationToken ct) => throw new NotImplementedException();
}
