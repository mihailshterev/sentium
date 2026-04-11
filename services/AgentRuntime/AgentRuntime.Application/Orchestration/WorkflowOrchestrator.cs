using AgentRuntime.Application.Workflows;
using AgentRuntime.Core.Agents;
using AgentRuntime.Core.Orchestration;
using AgentRuntime.Core.WorkflowManagement;
using AgentRuntime.Core.Workflows;
using Infrastructure.Messaging;

namespace AgentRuntime.Application.Orchestration;

public sealed class WorkflowOrchestrator(
    IAgentFactory factory,
    IAgentRegistry registry,
    IAgentManager agentManager,
    IWorkflowService workflowService,
    IEventBus nats) : IOrchestrator
{
    public async Task<WorkflowResult> RunAsync(WorkflowTrigger trigger, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        IAgentWorkflow workflow = trigger.TriggerType switch
        {
            WorkflowEvents.NetworkScan => new NetworkAnalysisWorkflow(factory, nats),
            WorkflowEvents.CustomWorkflow => new DynamicCustomWorkflow(factory, agentManager, workflowService, nats),
            _ => new DynamicDiscoveryWorkflow(factory, registry, agentManager, nats)
        };

        return await workflow.ExecuteAsync(trigger, ct);
    }
}
