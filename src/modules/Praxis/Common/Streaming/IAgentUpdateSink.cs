namespace Sentium.Praxis.Common.Streaming;

/// <summary>
/// Where <see cref="AgentRunStream"/> sends each update of a run: the assistant's SSE response
/// (<see cref="SseStreamWriter"/>) or a workflow's relay stream and persisted log
/// (<see cref="Features.Orchestration.RelayUpdateSink"/>).
/// </summary>
public interface IAgentUpdateSink
{
    Task WriteUpdateAsync(string type, string content, CancellationToken ct);
}
