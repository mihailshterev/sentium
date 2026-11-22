using System.Threading.Channels;

namespace Sentium.Praxis.Features.Assistant;

/// <summary>
/// In-memory, unbounded FIFO between the chat endpoints and <see cref="AssistantTurnProcessor"/>. Turns queued when
/// the process stops are ended with an error frame, never resumed.
/// </summary>
internal sealed class AssistantTurnQueue(AssistantTurnRegistry registry)
{
    private readonly Channel<AssistantTurn> _channel = Channel.CreateUnbounded<AssistantTurn>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    public ChannelReader<AssistantTurn> Reader => _channel.Reader;

    public void Enqueue(AssistantTurn turn)
    {
        ArgumentNullException.ThrowIfNull(turn);

        registry.Track(turn.TurnId, turn.UserId);
        if (!_channel.Writer.TryWrite(turn))
        {
            registry.Release(turn.TurnId);
            throw new InvalidOperationException("The assistant is shutting down and accepts no new turns.");
        }
    }
}
