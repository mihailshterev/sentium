namespace Sentium.Praxis.Common.Streaming;

/// <summary>
/// Receives each update of an agent run as a typed text frame (see <see cref="AgentUpdateTypes"/>).
/// </summary>
public interface IAgentUpdateSink
{
    Task WriteUpdateAsync(string type, string content, CancellationToken ct);
}
