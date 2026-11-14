namespace Sentium.Mcp.Entities;

/// <summary>
/// One tool of an <see cref="McpServer"/>. Agents only ever see the definition an admin approved: a tool is usable only
/// while <see cref="Enabled"/>, present on the server, and its current <see cref="DefinitionHash"/> equals
/// <see cref="ApprovedHash"/>.
/// </summary>
public sealed class McpToolEntry
{
    /// <summary>
    /// The tool's name on the MCP server.
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// The model-facing name (<c>{server}__{tool}</c>, sanitized); fixed when the tool is first discovered.
    /// </summary>
    public string QualifiedName { get; set; } = null!;

    public string? Title { get; set; }

    public string? Description { get; set; }

    /// <summary>
    /// The tool's input JSON schema, serialized.
    /// </summary>
    public string InputSchema { get; set; } = "{\"type\":\"object\"}";

    public bool? ReadOnlyHint { get; set; }

    public bool? DestructiveHint { get; set; }

    public bool? IdempotentHint { get; set; }

    public bool? OpenWorldHint { get; set; }

    /// <summary>
    /// Hash of the tool's current definition (name, title, description, schemas, annotations).
    /// </summary>
    public string DefinitionHash { get; set; } = null!;

    /// <summary>
    /// Hash of the definition an admin approved; <see langword="null"/> = never approved.
    /// </summary>
    public string? ApprovedHash { get; set; }

    /// <summary>
    /// The definition that was approved (description and schema), kept so a changed tool can be diffed in the portal.
    /// </summary>
    public string? ApprovedDescription { get; set; }

    public bool Enabled { get; set; }

    /// <summary>
    /// Whether each call waits for a human. Approval-gated tools are not offered to unattended workflow runs.
    /// </summary>
    public bool RequiresApproval { get; set; } = true;

    /// <summary>
    /// The server no longer lists this tool (as of the last sync).
    /// </summary>
    public bool Missing { get; set; }
}
