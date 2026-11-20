using System.Collections.Concurrent;

namespace Sentium.Praxis.Features.Orchestration;

internal sealed class WorkflowExecutionRegistry : IWorkflowExecutionRegistry
{
    private readonly ConcurrentDictionary<string, Run> _running = new(StringComparer.Ordinal);

    public void Register(string streamId, Guid? ownerId, CancellationTokenSource cts, string? label = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);
        ArgumentNullException.ThrowIfNull(cts);
        _running[streamId] = new Run(ownerId, cts, label, DateTimeOffset.UtcNow);
    }

    public void Unregister(string streamId)
    {
        if (!string.IsNullOrWhiteSpace(streamId))
        {
            _running.TryRemove(streamId, out _);
        }
    }

    public bool Cancel(string streamId)
    {
        return !string.IsNullOrWhiteSpace(streamId) && _running.TryGetValue(streamId, out var run) && TryCancel(run);
    }

    public bool CancelOwned(string streamId, Guid ownerId)
    {
        return !string.IsNullOrWhiteSpace(streamId) && _running.TryGetValue(streamId, out var run) && run.OwnerId == ownerId && TryCancel(run);
    }

    public IReadOnlyList<RunningWorkflow> Snapshot()
    {
        return [.. _running.Select(entry => new RunningWorkflow(entry.Key, entry.Value.OwnerId, entry.Value.Label, entry.Value.StartedAt))];
    }

    private static bool TryCancel(Run run)
    {
        try
        {
            run.Cts.Cancel();
            return true;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    private sealed record Run(Guid? OwnerId, CancellationTokenSource Cts, string? Label, DateTimeOffset StartedAt);
}
