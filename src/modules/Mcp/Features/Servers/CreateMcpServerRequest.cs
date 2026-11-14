using Sentium.Mcp.Entities;

namespace Sentium.Mcp.Features.Servers;

/// <param name="Name">Immutable slug, prefix of the server's tool names.</param>
/// <param name="Secrets">HTTP headers (<see cref="McpTransport.Http"/>) or environment variables (<see cref="McpTransport.SandboxStdio"/>), stored encrypted.</param>
public sealed record CreateMcpServerRequest(
    string Name,
    string DisplayName,
    string? Description,
    McpTransport Transport,
    string? Endpoint,
    string? Image,
    IReadOnlyList<string>? Args,
    string? Network,
    int? MemoryMb,
    double? CpuLimit,
    IReadOnlyDictionary<string, string>? Secrets,
    bool Enabled = true,
    IReadOnlyDictionary<string, string>? Variables = null);
