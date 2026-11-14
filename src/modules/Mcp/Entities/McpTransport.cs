using System.Text.Json.Serialization;

namespace Sentium.Mcp.Entities;

[JsonConverter(typeof(JsonStringEnumConverter<McpTransport>))]
public enum McpTransport
{
    /// <summary>
    /// A Streamable HTTP MCP server on the local network.
    /// </summary>
    Http,

    /// <summary>
    /// A stdio MCP server run by Sentium.Sandbox in a hardened container.
    /// </summary>
    SandboxStdio
}
