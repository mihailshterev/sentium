using Sentium.Praxis.Features.Agents;
using Sentium.Praxis.Features.Orchestration;
using Sentium.Praxis.Features.Workflows;

namespace Sentium.Praxis.Common.Streaming;

public static class AgentStreamExtensions
{
    public static void Emit(this IAgentStreamPublisher stream, string streamId, string author, string text, string updateType = AgentUpdateTypes.Message)
    {
        ArgumentNullException.ThrowIfNull(stream);
        stream.Publish(streamId, new AgentStreamUpdate(author, text, updateType));
    }
}
