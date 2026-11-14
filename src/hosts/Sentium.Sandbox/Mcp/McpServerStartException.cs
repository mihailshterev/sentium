namespace Sentium.Sandbox.Mcp;

/// <summary>
/// The server container could not be started or did not complete the MCP handshake.
/// </summary>
public sealed class McpServerStartException : Exception
{
    public McpServerStartException()
    {
    }

    public McpServerStartException(string message) : base(message)
    {
    }

    public McpServerStartException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
