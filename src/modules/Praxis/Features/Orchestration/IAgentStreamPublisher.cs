using Sentium.Praxis.Common.Streaming;

namespace Sentium.Praxis.Features.Orchestration;

/// <summary>
/// Write side of the agent stream: workflows push every update for a run here, keyed by its stream id.
/// </summary>
public interface IAgentStreamPublisher
{
    /// <summary>
    /// Appends <paramref name="update"/> to the stream's replay buffer and fans it out to live subscribers.
    /// Never blocks: slow subscribers drop their oldest queued frames instead of stalling the producer.
    /// </summary>
    void Publish(string streamId, AgentStreamUpdate update);
}
