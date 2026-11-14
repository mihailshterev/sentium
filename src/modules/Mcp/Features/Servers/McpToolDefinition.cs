namespace Sentium.Mcp.Features.Servers;

public sealed record McpToolDefinition(
    string Name,
    string? Title,
    string? Description,
    string InputSchema,
    bool? ReadOnlyHint,
    bool? DestructiveHint,
    bool? IdempotentHint,
    bool? OpenWorldHint,
    string Hash);
