using Sentium.AgentRuntime.Application.Workflows;
using Sentium.AgentRuntime.Core.Agents;
using Sentium.AgentRuntime.Core.Orchestration;
using Sentium.AgentRuntime.Core.WorkflowManagement;
using Sentium.AgentRuntime.Core.Workflows;
using Sentium.Infrastructure.Messaging;

namespace Sentium.AgentRuntime.Application.Orchestration;

public sealed class WorkflowOrchestrator(
    IAgentFactory factory,
    IAgentRegistry registry,
    IAgentRepository agentRepository,
    IWorkflowService workflowService,
    IEventBus nats) : IOrchestrator
{
    public async Task<WorkflowResult> RunAsync(WorkflowTrigger trigger, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        IAgentWorkflow workflow = trigger.TriggerType switch
        {
            WorkflowEvents.CustomWorkflow => new DynamicCustomWorkflow(factory, agentRepository, workflowService, nats),
            _ => new DynamicDiscoveryWorkflow(factory, registry, agentRepository, nats)
        };

        return await workflow.ExecuteAsync(trigger, ct);
    }
}
