using Sentium.Praxis.Common.Streaming;

namespace Sentium.Praxis.Features.Orchestration;

/// <summary>
/// Sends a workflow agent's updates to the run's relay stream and to its persisted log, under one author.
/// </summary>
public sealed class RelayUpdateSink(IAgentStreamPublisher stream, StreamLogAccumulator log, string streamId, string author) : IAgentUpdateSink
{
    public Task WriteUpdateAsync(string type, string content, CancellationToken ct)
    {
        stream.Emit(streamId, author, content, type);
        log.Add(author, content, type);
        return Task.CompletedTask;
    }
}
