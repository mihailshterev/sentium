using Sentium.Praxis.Features.Workflows;
using Sentium.Praxis.Features.Agents;
using Sentium.Praxis.Features.WorkflowManagement;

namespace Sentium.Praxis.Features.Orchestration;

public sealed class WorkflowOrchestrator(
    IAgentFactory factory,
    IAgentRegistry registry,
    IAgentRepository agentRepository,
    IWorkflowService workflowService,
    IAgentStreamPublisher stream) : IOrchestrator
{
    public async Task<WorkflowResult> RunAsync(WorkflowTrigger trigger, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        IAgentWorkflow workflow = trigger.TriggerType switch
        {
            WorkflowTriggerTypes.CustomWorkflow => new DynamicCustomWorkflow(factory, agentRepository, workflowService, stream),
            _ => new DynamicDiscoveryWorkflow(factory, registry, agentRepository, stream)
        };

        return await workflow.ExecuteAsync(trigger, ct);
    }
}
