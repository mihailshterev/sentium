namespace Sentium.Mcp.Features.Servers;

/// <param name="Name">The tool's name on the MCP server.</param>
/// <param name="Approve">Approve the tool's current definition.</param>
internal sealed record McpToolUpdate(string Name, bool? Enabled = null, bool? Approve = null, bool? RequiresApproval = null);
