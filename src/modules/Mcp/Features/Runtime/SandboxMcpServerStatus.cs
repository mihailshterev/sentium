namespace Sentium.Mcp.Features.Runtime;

/// <summary>
/// Runtime state of a Sandbox-hosted MCP server container.
/// </summary>
internal sealed record SandboxMcpServerStatus(
    string Name,
    bool Running,
    string? ContainerId,
    DateTimeOffset? StartedAt,
    DateTimeOffset? LastUsedAt,
    string? LastError,
    IReadOnlyList<string> StderrTail);
