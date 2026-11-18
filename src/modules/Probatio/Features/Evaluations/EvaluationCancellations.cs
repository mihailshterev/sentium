using System.Collections.Concurrent;

namespace Sentium.Probatio.Features.Evaluations;

/// <summary>
/// Cancellation of the Evaluation being worked on, so a cancel request from any HTTP scope reaches it.
/// </summary>
internal sealed class EvaluationCancellations
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> active = new();

    public CancellationTokenSource Register(Guid evaluationId, CancellationToken stoppingToken)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        active[evaluationId] = source;
        return source;
    }

    public void Unregister(Guid evaluationId) => active.TryRemove(evaluationId, out _);

    /// <summary>
    /// Whether the Evaluation was being worked on and has now been told to stop.
    /// </summary>
    public bool Cancel(Guid evaluationId)
    {
        if (!active.TryGetValue(evaluationId, out var source))
        {
            return false;
        }

        try
        {
            source.Cancel();
            return true;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }
}
