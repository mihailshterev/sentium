using AgentRuntime.Application.Workflows;
using AgentRuntime.Core.Agents;
using AgentRuntime.Core.Orchestration;
using AgentRuntime.Core.Workflows;

namespace AgentRuntime.Application.Orchestration;

public sealed class AgentOrchestrator : IOrchestrator
{
    private readonly IAgentFactory AgentFactory;
    private readonly IAgentRegistry AgentRegistry;

    public AgentOrchestrator(IAgentFactory factory, IAgentRegistry registry)
    {
        AgentFactory = factory;
        AgentRegistry = registry;
    }

    public async Task<WorkflowResult> RunAsync(WorkflowTrigger trigger, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(trigger);

        IAgentWorkflow workflow = trigger.TriggerType switch
        {
            AgentEvents.NetworkScan => new NetworkAnalysisWorkflow(),
            _ => new DynamicDiscoveryWorkflow(AgentFactory, AgentRegistry)
        };

        return await workflow.ExecuteAsync(trigger, ct);
    }
}
