using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Sentium.Mcp.Features.Runtime;

/// <summary>
/// A Streamable HTTP server reached directly with the MCP SDK's <see cref="McpClient"/>. Owns the client and its
/// transport (and through it the HttpClient).
/// </summary>
internal sealed class DirectMcpConnection(McpClient client, IAsyncDisposable transport) : IMcpConnection
{
    public bool IsAlive => !client.Completion.IsCompleted;

    public async Task<IReadOnlyList<Tool>> ListToolsAsync(CancellationToken ct)
    {
        var tools = await client.ListToolsAsync(cancellationToken: ct);
        return [.. tools.Select(t => t.ProtocolTool)];
    }

    public async Task<CallToolResult> CallToolAsync(string toolName, IReadOnlyDictionary<string, object?> arguments, CancellationToken ct) =>
        await client.CallToolAsync(toolName, arguments, cancellationToken: ct);

    public async ValueTask DisposeAsync()
    {
        await client.DisposeAsync();
        await transport.DisposeAsync();
    }
}
