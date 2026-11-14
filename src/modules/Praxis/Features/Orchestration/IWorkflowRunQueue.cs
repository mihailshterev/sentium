using Sentium.Praxis.Features.Workflows;

namespace Sentium.Praxis.Features.Orchestration;

/// <summary>
/// Hands accepted workflow runs to <see cref="WorkflowRunProcessor"/>, decoupling the HTTP request that
/// triggers a run from its (long) execution.
/// </summary>
internal interface IWorkflowRunQueue
{
    ValueTask EnqueueAsync(WorkflowTrigger trigger, CancellationToken ct = default);
}
