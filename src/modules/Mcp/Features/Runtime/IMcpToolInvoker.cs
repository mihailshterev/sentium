using ModelContextProtocol.Protocol;

namespace Sentium.Mcp.Features.Runtime;

/// <summary>
/// Calls a tool on an MCP server (implemented by <see cref="McpClientManager"/>).
/// </summary>
public interface IMcpToolInvoker
{
    Task<CallToolResult> CallToolAsync(
        McpServerConnectionInfo server,
        string toolName,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken ct);
}
