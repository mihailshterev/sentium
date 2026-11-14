namespace Sentium.Registry.Features.Settings;

/// <summary>
/// Global settings for the MCP (Model Context Protocol) client, managed by Sovereign users and consumed by Praxis.
/// The servers themselves are Praxis data (registered and approved in the portal); these are the runtime knobs.
/// </summary>
internal sealed class McpSettings
{
    /// <summary>
    /// Kill switch. When false no agent is offered an MCP tool and open MCP connections are closed.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Time allowed to connect to (and initialize) an MCP server, in seconds.
    /// </summary>
    public int ConnectTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Time allowed for a single MCP tool call, in seconds.
    /// </summary>
    public int CallTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// An MCP connection unused for this long is closed (a Sandbox-hosted server container is stopped with it).
    /// </summary>
    public int IdleTimeoutMinutes { get; set; } = 15;

    /// <summary>
    /// Upper bound on the text an MCP tool result may put into the conversation; longer results are truncated.
    /// </summary>
    public int MaxResultChars { get; set; } = 16_000;
}
