namespace Sentium.Praxis.Contracts;

/// <summary>
/// Starts Schedule runs on the workflow run queue, so they get the same retry, timeout, cancellation, history and
/// terminal frame as a run started from the portal. The run acts as the Schedule's owner.
/// </summary>
public interface IUnattendedRunLauncher
{
    /// <summary>
    /// Queues the run, unless the same Schedule's previous run has not ended yet. A missing agent or workflow is not
    /// checked here: the run itself fails with a clear reason, so the owner is notified.
    /// </summary>
    Task<UnattendedRunLaunch> LaunchAsync(UnattendedRunRequest request, CancellationToken ct = default);

    /// <summary>
    /// The agent (by roster name) or saved workflow the owner would run, or <see langword="null"/> when the owner has no
    /// such agent or workflow.
    /// </summary>
    Task<UnattendedRunTarget?> ResolveTargetAsync(UnattendedRunKind kind, string? agentName, Guid? workflowId, Guid ownerId, CancellationToken ct = default);
}
