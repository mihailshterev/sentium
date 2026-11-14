using Sentium.Praxis.Features.Workflows;

namespace Sentium.Praxis.Features.Orchestration;

internal interface IOrchestrator
{
    Task<WorkflowResult> RunAsync(WorkflowTrigger trigger, CancellationToken ct = default);
}
