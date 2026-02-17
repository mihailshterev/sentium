using AgentRuntime.Application.Workflows;
using AgentRuntime.Core.Agents;
using AgentRuntime.Core.Orchestration;
using AgentRuntime.Core.Workflows;
using Infrastructure.Messaging;

namespace AgentRuntime.Application.Orchestration;

public sealed class AgentOrchestrator : IOrchestrator
{
    private readonly IAgentFactory AgentFactory;
    private readonly IAgentRegistry AgentRegistry;
    private readonly IEventBus Nats;

    public AgentOrchestrator(IAgentFactory factory, IAgentRegistry registry, IEventBus nats)
    {
        AgentFactory = factory;
        AgentRegistry = registry;
        Nats = nats;
    }

    public async Task<WorkflowResult> RunAsync(WorkflowTrigger trigger, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(trigger);

        IAgentWorkflow workflow = trigger.TriggerType switch
        {
            AgentEvents.NetworkScan => new NetworkAnalysisWorkflow(this.AgentFactory, this.AgentRegistry, this.Nats),
            _ => new DynamicDiscoveryWorkflow(AgentFactory, AgentRegistry)
        };

        return await workflow.ExecuteAsync(trigger, ct);
    }
}
