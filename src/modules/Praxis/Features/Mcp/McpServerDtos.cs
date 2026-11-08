using Sentium.Praxis.Entities;

namespace Sentium.Praxis.Features.Mcp;

/// <summary>
/// An MCP server as shown in the portal. Secret values are never returned - only their names; non-secret
/// <paramref name="Variables"/> are returned with their values.
/// </summary>
public sealed record McpServerDto(
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

/// <param name="Name">The tool's name on the MCP server.</param>
/// <param name="QualifiedName">The name agents use (<c>{server}__{tool}</c>).</param>
/// <param name="ApprovedDescription">The description as approved; differs from <paramref name="Description"/> when the tool changed.</param>
/// <param name="Approved">The current definition is the approved one.</param>
/// <param name="Changed">The tool was approved before, but its definition has changed since (needs re-approval).</param>
/// <param name="Missing">The server no longer lists the tool.</param>
/// <param name="Usable">Agents can currently be given the tool.</param>
public sealed record McpToolDto(
    string Name,
    string QualifiedName,
    string? Title,
    string? Description,
    string? ApprovedDescription,
    string InputSchema,
    bool? ReadOnlyHint,
    bool? DestructiveHint,
    bool? IdempotentHint,
    bool? OpenWorldHint,
    bool Enabled,
    bool RequiresApproval,
    bool Approved,
    bool Changed,
    bool Missing,
    bool Usable);

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

/// <param name="Variables">Like <paramref name="Secrets"/> but stored in plain text and shown in the portal; a name is either a secret or a variable.</param>
/// <param name="Secrets">Changes to the stored secrets: <see langword="null"/> keeps them all; an entry with a value sets it, an entry with a <see langword="null"/> value removes it, unmentioned names are kept.</param>
public sealed record UpdateMcpServerRequest(
    string DisplayName,
    string? Description,
    string? Endpoint,
    string? Image,
    IReadOnlyList<string>? Args,
    string? Network,
    int? MemoryMb,
    double? CpuLimit,
    IReadOnlyDictionary<string, string?>? Secrets,
    bool Enabled,
    IReadOnlyDictionary<string, string?>? Variables = null);

public sealed record UpdateMcpToolsRequest(IReadOnlyList<McpToolUpdate> Tools);

/// <param name="Name">The tool's name on the MCP server.</param>
/// <param name="Approve">Approve the tool's current definition.</param>
public sealed record McpToolUpdate(string Name, bool? Enabled = null, bool? Approve = null, bool? RequiresApproval = null);

/// <summary>
/// Live state of an MCP server: the Praxis connection and, for Sandbox-hosted servers, the container.
/// </summary>
public sealed record McpServerStatusDto(McpConnectionState Connection, SandboxMcpServerStatus? Sandbox, string? SandboxError);
