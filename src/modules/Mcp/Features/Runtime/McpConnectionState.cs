namespace Sentium.Mcp.Features.Runtime;

/// <summary>
/// Live connection state of one MCP server, for the portal.
/// </summary>
internal sealed record McpConnectionState(bool Connected, DateTimeOffset? ConnectedAt, DateTimeOffset? LastUsedAt, string? LastError);
