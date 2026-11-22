using Sentium.Praxis.Common.Streaming;
using Sentium.Praxis.Features.Orchestration;

namespace Sentium.Praxis.Features.Assistant;

internal sealed class RelayUpdateSink(IAgentStreamPublisher publisher, string streamId) : IAgentUpdateSink
{
    public const string AssistantAuthor = "Assistant";

    public Task WriteUpdateAsync(string type, string content, CancellationToken ct)
    {
        publisher.Publish(streamId, new AgentStreamUpdate(AssistantAuthor, content, type));
        return Task.CompletedTask;
    }
}
