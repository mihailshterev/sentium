namespace Sentium.Mcp.Features.Servers;

internal sealed record UpdateMcpToolsRequest(IReadOnlyList<McpToolUpdate> Tools);
