using Sentium.Mcp.Entities;
using Sentium.Mcp.Features.Runtime;

namespace Sentium.Mcp.Features.Servers;

/// <summary>
/// An MCP server as shown in the portal. Secret values are never returned - only their names; non-secret
/// <paramref name="Variables"/> are returned with their values.
/// </summary>
internal sealed record McpServerDto(
    Guid Id,
    string Name,
    string DisplayName,
    string? Description,
    McpTransport Transport,
    string? Endpoint,
    string? Image,
    IReadOnlyList<string> Args,
    string? Network,
    int? MemoryMb,
    double? CpuLimit,
    IReadOnlyList<string> SecretNames,
    IReadOnlyDictionary<string, string> Variables,
    bool Enabled,
    IReadOnlyList<McpToolDto> Tools,
    DateTimeOffset? LastSyncedAt,
    string? LastError,
    McpConnectionState Connection,
    DateTime CreatedAt,
    DateTime UpdatedAt);
