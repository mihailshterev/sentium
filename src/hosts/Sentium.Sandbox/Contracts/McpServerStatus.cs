namespace Sentium.Sandbox.Contracts;

/// <summary>
/// Runtime state of a hosted MCP server.
/// </summary>
public sealed record McpServerStatus(
    string Name,
    bool Running,
    string? ContainerId,
    DateTimeOffset? StartedAt,
    DateTimeOffset? LastUsedAt,
    string? LastError,
    IReadOnlyList<string> StderrTail);
