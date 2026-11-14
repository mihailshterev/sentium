namespace Sentium.Mcp.Features.Servers;

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
