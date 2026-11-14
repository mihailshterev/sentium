using ModelContextProtocol.Protocol;

namespace Sentium.Mcp.Features.Runtime;

internal interface IMcpConnection : IAsyncDisposable
{
    bool IsAlive { get; }

    Task<IReadOnlyList<Tool>> ListToolsAsync(CancellationToken ct);

    Task<CallToolResult> CallToolAsync(string toolName, IReadOnlyDictionary<string, object?> arguments, CancellationToken ct);
}
