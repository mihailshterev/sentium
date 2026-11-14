namespace Sentium.Mcp.Entities;

/// <summary>
/// An MCP (Model Context Protocol) server registered by a Sovereign. Global - not user-owned: every user can assign its
/// approved tools to their agents.
/// </summary>
public sealed class McpServer
{
    public Guid Id { get; set; }

    /// <summary>
    /// Immutable slug (<c>^[a-z0-9][a-z0-9-]{1,23}$</c>); prefixes the server's tool names (<c>{name}__{tool}</c>), which
    /// agents persist, so it never changes after creation.
    /// </summary>
    public string Name { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public string? Description { get; set; }

    public McpTransport Transport { get; set; }

    /// <summary>
    /// Streamable HTTP endpoint (<see cref="McpTransport.Http"/>); must resolve to a local-network address.
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// Digest-pinned container image (<see cref="McpTransport.SandboxStdio"/>).
    /// </summary>
    public string? Image { get; set; }

    /// <summary>
    /// Arguments passed to the image's entrypoint (<see cref="McpTransport.SandboxStdio"/>).
    /// </summary>
    public List<string> Args { get; set; } = [];

    /// <summary>
    /// Docker network the server container joins; <see langword="null"/> = no network. Must be one the Sandbox operator
    /// allows (<c>Sandbox:Mcp:AllowedNetworks</c>).
    /// </summary>
    public string? Network { get; set; }

    public int? MemoryMb { get; set; }

    public double? CpuLimit { get; set; }

    /// <summary>
    /// Data-Protection-protected JSON map of secret values: HTTP headers (<see cref="McpTransport.Http"/>) or
    /// environment variables (<see cref="McpTransport.SandboxStdio"/>). Never returned by the API.
    /// </summary>
    public string? ProtectedSecrets { get; set; }

    /// <summary>
    /// Non-secret values (HTTP headers or environment variables) stored in plain text so the portal can show and edit
    /// them. A name lives here or in <see cref="ProtectedSecrets"/>, never both. Replace the dictionary instead of
    /// mutating it so EF detects the change.
    /// </summary>
    public Dictionary<string, string> Variables { get; set; } = [];

    public bool Enabled { get; set; }

    /// <summary>
    /// The server's tools as last discovered, with their approval state (stored as JSON).
    /// </summary>
    public List<McpToolEntry> Tools { get; set; } = [];

    public DateTimeOffset? LastSyncedAt { get; set; }

    public string? LastError { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
