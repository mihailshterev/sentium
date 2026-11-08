namespace Sentium.Sandbox.Options;

/// <summary>
/// The Sandbox's MCP host: long-lived, hardened containers running stdio MCP servers on Sentium.Web's behalf
/// (section <c>Sandbox:Mcp</c>). Every limit here is an operator ceiling - what a Sovereign configures in the portal
/// cannot exceed it. Which image runs is the Sovereign's call (trusted-household model); only digest pinning is
/// enforced.
/// </summary>
public sealed class McpHostOptions
{
    public const string SectionName = "Sandbox:Mcp";

    /// <summary>
    /// Off by default: running MCP servers is opt-in per deployment.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Docker networks an MCP server container may join. Empty = containers never get a network. Docker cannot limit a
    /// container's egress to the local network, so list only <c>internal: true</c> networks (or ones the host firewall
    /// restricts) - a gateway such as SearXNG can sit on such a network and a normal one.
    /// </summary>
    public List<string> AllowedNetworks { get; set; } = [];

    /// <summary>
    /// Images must be referenced by digest (<c>repo@sha256:…</c>) so a tag cannot be repointed underneath us.
    /// </summary>
    public bool RequireDigestPinnedImages { get; set; } = true;

    public bool PullImageIfMissing { get; set; } = true;

    public int MaxServers { get; set; } = 8;

    public int DefaultMemoryMb { get; set; } = 256;

    public int MaxMemoryMb { get; set; } = 1_024;

    public double DefaultCpuLimit { get; set; } = 0.5;

    public double MaxCpuLimit { get; set; } = 2;

    public long PidsLimit { get; set; } = 256;

    public string TmpfsSizeMb { get; set; } = "128m";

    /// <summary>
    /// Forces the user the server process runs as (e.g. <c>65534:65534</c>). Empty = the image's own user: many MCP
    /// images only make their entrypoint executable for that user. Either way the container has no capabilities,
    /// no-new-privileges, a read-only root filesystem and (by default) no network.
    /// </summary>
    public string RunAsUser { get; set; } = string.Empty;

    /// <summary>
    /// A server unused for this long is stopped (Sentium.Web restarts it on the next call).
    /// </summary>
    public int IdleTimeoutMinutes { get; set; } = 30;

    public int InitializationTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Upper bound on one stdout line (one MCP message); a server exceeding it is treated as broken.
    /// </summary>
    public int MaxMessageBytes { get; set; } = 8 * 1024 * 1024;

    /// <summary>
    /// How many recent stderr lines are kept per server for diagnostics.
    /// </summary>
    public int StderrTailLines { get; set; } = 50;
}
