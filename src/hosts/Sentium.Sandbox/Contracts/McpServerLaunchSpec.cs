namespace Sentium.Sandbox.Contracts;

/// <summary>
/// How to run a stdio MCP server container. Sent by Sentium.Web (Praxis) with the secrets already decrypted; held in
/// memory only.
/// </summary>
/// <param name="Image">Container image, pinned by digest.</param>
/// <param name="Args">Arguments for the image's entrypoint (empty = the image's default command).</param>
/// <param name="Env">Environment variables, typically the server's credentials.</param>
/// <param name="Network">Docker network to join (must be allowed by <c>Sandbox:Mcp:AllowedNetworks</c>); null = none.</param>
internal sealed record McpServerLaunchSpec(
    string Image,
    IReadOnlyList<string>? Args,
    IReadOnlyDictionary<string, string>? Env,
    string? Network,
    int? MemoryMb,
    double? CpuLimit);
