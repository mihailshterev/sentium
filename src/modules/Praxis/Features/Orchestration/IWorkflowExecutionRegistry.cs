namespace Sentium.Praxis.Features.Orchestration;

/// <summary>
/// Tracks the in-flight workflow executions so a user's stop request (<c>POST orchestration/cancel/{streamId}</c>)
/// can reach the run's <see cref="CancellationTokenSource"/>. Cancelling a stream that is not running is a no-op.
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
    /// <see langword="true"/> if the run was in flight and a cancellation was signalled.
    /// </summary>
    bool Cancel(string streamId);
}
