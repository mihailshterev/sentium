using System.Threading.Channels;

namespace Sentium.Probatio.Features.Evaluations;

/// <summary>
/// Ids of queued Evaluations, worked through one at a time by <see cref="EvaluationWorker"/>. In memory only: on
/// startup the worker re-queues whatever the database still has queued.
/// </summary>
internal sealed class EvaluationQueue
{
    private readonly Channel<Guid> channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = true });

    public void Enqueue(Guid evaluationId) => channel.Writer.TryWrite(evaluationId);

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct) => channel.Reader.ReadAllAsync(ct);
}
