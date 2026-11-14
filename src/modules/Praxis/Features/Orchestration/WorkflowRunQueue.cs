using System.Threading.Channels;
using Sentium.Praxis.Features.Workflows;

namespace Sentium.Praxis.Features.Orchestration;

/// <summary>
/// In-memory, unbounded FIFO. Runs queued or executing when the process stops are not resumed; the
/// processor records them as interrupted so they still show up in the run history.
/// </summary>
public sealed class WorkflowRunQueue : IWorkflowRunQueue
{
    private readonly Channel<WorkflowTrigger> _channel = Channel.CreateUnbounded<WorkflowTrigger>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    public ChannelReader<WorkflowTrigger> Reader => _channel.Reader;

    public ValueTask EnqueueAsync(WorkflowTrigger trigger, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        return _channel.Writer.WriteAsync(trigger, ct);
    }
}
