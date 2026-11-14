using Sentium.Mcp.Features.Runtime;

namespace Sentium.Mcp.Features.Servers;

/// <summary>
/// Live state of an MCP server: the Praxis connection and, for Sandbox-hosted servers, the container.
/// </summary>
internal sealed record McpServerStatusDto(McpConnectionState Connection, SandboxMcpServerStatus? Sandbox, string? SandboxError);
