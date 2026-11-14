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
    /// completing when a terminal (Done/Error) frame has been delivered or <paramref name="ct"/> fires.
    /// </summary>
    IAsyncEnumerable<StreamFrame> Subscribe(string streamId, long fromSeqExclusive, CancellationToken ct);
}
