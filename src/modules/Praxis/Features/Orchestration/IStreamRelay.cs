using Sentium.Praxis.Common.Streaming;

namespace Sentium.Praxis.Features.Orchestration;

/// <summary>
/// In-process fan-out + replay buffer for agent stream updates. Producers write through
/// <see cref="IAgentStreamPublisher"/>; the stream's buffer exists from the first update, so nothing is lost
/// when the SSE client connects after the run already started. SSE consumers attach via <see cref="Subscribe"/>
/// and receive the full backlog (filtered by <c>Last-Event-ID</c>) followed by live updates, which also makes
/// EventSource reconnects replay cleanly without duplicates.
/// </summary>
internal interface IStreamRelay
{
    /// <summary>
    /// Replays buffered frames with <c>Seq &gt; <paramref name="fromSeqExclusive"/></c> then yields live frames,
    /// completing when a terminal (Done/Error) frame has been delivered or <paramref name="ct"/> fires. Yields nothing
    /// for a stream the relay does not hold, and never creates one.
    /// </summary>
    IAsyncEnumerable<StreamFrame> Subscribe(string streamId, long fromSeqExclusive, CancellationToken ct);

    /// <summary>
    /// Records the user whose run the stream carries; call before the run is queued.
    /// </summary>
    void Claim(string streamId, Guid ownerId);

    /// <summary>
    /// Whether <paramref name="userId"/> may read the stream: it is theirs, nobody has claimed it, or the relay does not
    /// hold it (reading an unknown stream yields only a "gone" frame).
    /// </summary>
    bool IsVisibleTo(string streamId, Guid userId);

    /// <summary>
    /// Whether the relay still holds the stream: it was claimed or published to and not yet evicted.
    /// </summary>
    bool Exists(string streamId);
}
