using System.Collections.Concurrent;

namespace Sentium.AgentRuntime.Application.Orchestration;

/// <summary>
/// Tracks the in-flight workflow executions on this instance so an out-of-band cancellation request
/// (delivered over NATS, see <see cref="Sentium.AgentRuntime.Core.Workflows.WorkflowEvents.CancelSignal"/>)
/// can reach the run's <see cref="CancellationTokenSource"/>. Only the instance actually executing a given
/// stream holds its token source; cancel requests on other instances are simply no-ops there.
/// </summary>
public interface IWorkflowExecutionRegistry
{
    /// <summary>
    /// Registers the token source driving the run identified by <paramref name="streamId"/>.
    /// </summary>
    void Register(string streamId, CancellationTokenSource cts);

    /// <summary>
    /// Removes the run's registration (call once the run reaches a terminal state).
    /// </summary>
    void Unregister(string streamId);

    /// <summary>
    /// Requests cancellation of the run identified by <paramref name="streamId"/>. Returns
    /// <see langword="true"/> if the run was tracked on this instance and a cancellation was signalled.
    /// </summary>
    bool Cancel(string streamId);
}

public sealed class WorkflowExecutionRegistry : IWorkflowExecutionRegistry
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
