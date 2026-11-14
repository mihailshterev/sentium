namespace Sentium.Mcp.Features.Servers;

public sealed record UpdateMcpToolsRequest(IReadOnlyList<McpToolUpdate> Tools);
