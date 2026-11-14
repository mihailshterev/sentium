namespace Sentium.Mcp.Features.Runtime;

/// <summary>
/// The Sandbox does not know the server (never ensured, or the Sandbox restarted) - ensure it and retry.
/// </summary>
public sealed class SandboxMcpServerUnknownException : Exception
{
    public SandboxMcpServerUnknownException()
    {
    }

    public SandboxMcpServerUnknownException(string message) : base(message)
    {
    }

    public SandboxMcpServerUnknownException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
