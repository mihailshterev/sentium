namespace Sentium.Sandbox.Mcp;

/// <summary>
/// A launch request the operator's limits do not allow (image or network not allow-listed, too many servers, ...).
/// </summary>
public sealed class McpHostRejectedException : Exception
{
    public McpHostRejectedException()
    {
    }

    public McpHostRejectedException(string message) : base(message)
    {
    }

    public McpHostRejectedException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
