using System.Collections.Concurrent;

namespace Sentium.Praxis.Features.Orchestration;

internal sealed class WorkflowExecutionRegistry : IWorkflowExecutionRegistry
{
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _running = new(StringComparer.Ordinal);

    public void Register(string streamId, CancellationTokenSource cts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);
        ArgumentNullException.ThrowIfNull(cts);
        _running[streamId] = cts;
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
        if (string.IsNullOrWhiteSpace(streamId) || !_running.TryGetValue(streamId, out var cts))
        {
            return false;
        }

        try
        {
            cts.Cancel();
            return true;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }
}
