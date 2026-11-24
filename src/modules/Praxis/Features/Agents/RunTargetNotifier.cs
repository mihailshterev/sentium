using Sentium.Praxis.Contracts;

namespace Sentium.Praxis.Features.Agents;

/// <summary>
/// Tells every <see cref="IRunTargetObserver"/> about a renamed or deleted agent or workflow. The change is already
/// committed, so one observer's failure is logged and never undoes it or stops the others.
/// </summary>
internal sealed class RunTargetNotifier(IEnumerable<IRunTargetObserver> observers, ILogger<RunTargetNotifier> logger)
{
    private readonly IReadOnlyList<IRunTargetObserver> _observers = [.. observers];

    public Task AgentRenamedAsync(Guid ownerId, string oldName, string newName, CancellationToken ct) =>
        NotifyAsync(o => o.OnAgentRenamedAsync(ownerId, oldName, newName, ct), "renamed agent", oldName);

    public Task AgentDeletedAsync(Guid ownerId, string agentName, CancellationToken ct) =>
        NotifyAsync(o => o.OnAgentDeletedAsync(ownerId, agentName, ct), "deleted agent", agentName);

    public Task WorkflowDeletedAsync(Guid ownerId, Guid workflowId, CancellationToken ct) =>
        NotifyAsync(o => o.OnWorkflowDeletedAsync(ownerId, workflowId, ct), "deleted workflow", workflowId.ToString());

    private async Task NotifyAsync(Func<IRunTargetObserver, Task> notify, string change, string target)
    {
        foreach (var observer in _observers)
        {
            try
            {
                await notify(observer);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "{Observer} failed to follow the {Change} {Target}.", observer.GetType().Name, change, target);
            }
        }
    }
}
