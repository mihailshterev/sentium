namespace Sentium.Praxis.Contracts;

/// <summary>
/// Told when an agent or workflow that unattended runs can target is renamed or deleted, so whatever names it (a
/// Schedule) can follow instead of failing on every run. Implementations must not throw.
/// </summary>
public interface IRunTargetObserver
{
    Task OnAgentRenamedAsync(Guid ownerId, string oldName, string newName, CancellationToken ct);

    Task OnAgentDeletedAsync(Guid ownerId, string agentName, CancellationToken ct);

    Task OnWorkflowDeletedAsync(Guid ownerId, Guid workflowId, CancellationToken ct);
}
