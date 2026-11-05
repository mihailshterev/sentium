using Sentium.Praxis.Features.Workflows;

namespace Sentium.Praxis.Features.Orchestration;

/// <summary>
/// Dispatches a trigger to the registered <see cref="IAgentWorkflow"/> for its workflow type.
/// </summary>
public sealed class WorkflowOrchestrator(IEnumerable<IAgentWorkflow> workflows) : IOrchestrator
{
    public async Task<WorkflowResult> RunAsync(WorkflowTrigger trigger, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(trigger);

        var type = trigger.TriggerType == WorkflowTriggerTypes.CustomWorkflow ? WorkflowType.Custom : WorkflowType.Dynamic;
        var workflow = workflows.FirstOrDefault(w => w.Type == type)
            ?? throw new InvalidOperationException($"No workflow is registered for type '{type}'.");

        return await workflow.ExecuteAsync(trigger, ct);
    }
}
