namespace Sentium.Mcp.Features.Runtime;

/// <summary>
/// How Sentium.Sandbox should run a stdio MCP server container. Mirrors the Sandbox's <c>McpServerLaunchSpec</c>.
/// </summary>
public sealed record SandboxMcpLaunchSpec(
    string Image,
    IReadOnlyList<string> Args,
    IReadOnlyDictionary<string, string> Env,
    string? Network,
    int? MemoryMb,
    double? CpuLimit);
