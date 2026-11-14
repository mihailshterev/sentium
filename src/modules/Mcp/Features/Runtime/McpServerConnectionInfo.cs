using Sentium.Mcp.Entities;

namespace Sentium.Mcp.Features.Runtime;

/// <summary>
/// What the client manager needs to reach one MCP server. <see cref="Version"/> changes whenever the server is edited,
/// which makes the manager drop the old connection and reconnect with the new configuration.
/// </summary>
public sealed record McpServerConnectionInfo(
    Guid Id,
    string Name,
    McpTransport Transport,
    string? Endpoint,
    string? Image,
    IReadOnlyList<string> Args,
    string? Network,
    int? MemoryMb,
    double? CpuLimit,
    string? ProtectedSecrets,
    IReadOnlyDictionary<string, string> Variables,
    DateTime Version)
{
    public static McpServerConnectionInfo From(McpServer server)
    {
        ArgumentNullException.ThrowIfNull(server);

        return new(server.Id, server.Name, server.Transport, server.Endpoint, server.Image, [.. server.Args], server.Network,
            server.MemoryMb, server.CpuLimit, server.ProtectedSecrets, new Dictionary<string, string>(server.Variables, StringComparer.Ordinal), server.UpdatedAt);
    }
}
