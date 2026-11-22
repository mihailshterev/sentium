using Sentium.Praxis.Common.Streaming;

namespace Sentium.Praxis.Features.Orchestration;

/// <summary>
/// Write side of the agent stream: workflows push every update for a run here, keyed by its stream id.
/// </summary>
internal interface IAgentStreamPublisher
{
    /// <summary>
    /// Appends <paramref name="update"/> to the stream's replay buffer and fans it out to live subscribers; consecutive
    /// text tokens are joined into one frame first. Never blocks: a subscriber that falls too far behind is disconnected
    /// and catches up from the buffer when its client reconnects.
    /// </summary>
    void Publish(string streamId, AgentStreamUpdate update);
}
