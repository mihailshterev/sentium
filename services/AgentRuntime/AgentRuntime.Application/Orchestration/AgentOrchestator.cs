using AgentRuntime.Application.Workflows;
using AgentRuntime.Core.Agents;
using AgentRuntime.Core.Orchestration;
using AgentRuntime.Core.Workflows;
using Infrastructure.Messaging;

namespace AgentRuntime.Application.Orchestration;

public sealed class AgentOrchestrator(
    IAgentFactory factory,
    IAgentRegistry registry,
    IEventBus nats) : IOrchestrator
{
    public async Task<WorkflowResult> RunAsync(WorkflowTrigger trigger, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(trigger);

        IAgentWorkflow workflow = trigger.TriggerType switch
        {
            AgentEvents.NetworkScan => new NetworkAnalysisWorkflow(factory, nats),
            _ => new DynamicDiscoveryWorkflow(factory, registry, nats)
        };

        return await workflow.ExecuteAsync(trigger, ct);
    }
}
