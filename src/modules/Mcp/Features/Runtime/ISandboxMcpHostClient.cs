using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace Sentium.Mcp.Features.Runtime;

/// <summary>
/// REST client for the Sandbox's MCP host (<c>/mcp/servers/{name}</c>, internal-token authenticated). Tool payloads are
/// the MCP SDK's own protocol types, serialized with <see cref="McpJsonUtilities.DefaultOptions"/>.
/// </summary>
public interface ISandboxMcpHostClient
{
    Task<SandboxMcpServerStatus> EnsureAsync(string name, SandboxMcpLaunchSpec spec, CancellationToken ct);

    Task StopAsync(string name, CancellationToken ct);

    Task<SandboxMcpServerStatus?> GetStatusAsync(string name, CancellationToken ct);

    Task<ListToolsResult> ListToolsAsync(string name, CancellationToken ct);

    Task<CallToolResult> CallToolAsync(string name, CallToolRequestParams request, CancellationToken ct);
}
