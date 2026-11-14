using ModelContextProtocol.Protocol;
using Sentium.Sandbox.Contracts;

namespace Sentium.Sandbox.Mcp;

/// <summary>
/// Hosts stdio MCP servers for Sentium.Web.
/// </summary>
internal interface IMcpServerHost
{
    bool Enabled { get; }

    /// <summary>
    /// Starts the server (or keeps it running when the spec is unchanged). Throws <see cref="McpHostRejectedException"/>
    /// when the operator's limits forbid the spec and <see cref="McpServerStartException"/> when it fails to start.
    /// </summary>
    Task<McpServerStatus> EnsureAsync(string name, McpServerLaunchSpec spec, CancellationToken ct);

    /// <summary>
    /// The server's tools, or <see langword="null"/> when the server is unknown.
    /// </summary>
    Task<ListToolsResult?> ListToolsAsync(string name, CancellationToken ct);

    /// <summary>
    /// The tool's result, or <see langword="null"/> when the server is unknown.
    /// </summary>
    Task<CallToolResult?> CallToolAsync(string name, CallToolRequestParams request, CancellationToken ct);

    McpServerStatus? GetStatus(string name);

    Task<bool> StopAsync(string name);
}
