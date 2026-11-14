namespace Sentium.Mcp;

/// <summary>
/// HTTP route prefix for the MCP server endpoints hosted by Sentium.Web. MCP was split out of Praxis; the prefix stays
/// <c>api/praxis</c> so the portal's URLs are unchanged.
/// </summary>
internal static class McpRoutes
{
    public const string Prefix = "api/praxis";
}
