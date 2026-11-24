using System.Threading.Channels;

namespace Sentium.Praxis.Features.Conversations;

/// <summary>
/// Conversations whose summary may be due, handed from the end of a chat turn to <see cref="HistorySummaryWorker"/>
/// so the turn never waits on the extra model call. Bounded and lossy: a dropped request is made again after the next
/// turn of that conversation.
/// </summary>
internal sealed class HistorySummaryQueue
{
    private const int Capacity = 256;

    private readonly Channel<HistorySummaryRequest> _channel = Channel.CreateBounded<HistorySummaryRequest>(
        new BoundedChannelOptions(Capacity) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    public ChannelReader<HistorySummaryRequest> Reader => _channel.Reader;

    public void Enqueue(Guid conversationId, Guid userId) => _channel.Writer.TryWrite(new HistorySummaryRequest(conversationId, userId));
}
