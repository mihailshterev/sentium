namespace Sentium.Infrastructure.Net;

/// <summary>
/// Which destination addresses an outbound HTTP client may connect to.
/// </summary>
public enum NetworkAddressScope
{
    /// <summary>
    /// Loopback and private-network addresses only (RFC 1918, RFC 6598 shared space, IPv6 ULA) - e.g. MCP servers,
    /// which Sentium only ever reaches on the local network.
    /// </summary>
    PrivateOnly,

    /// <summary>
    /// Public internet addresses only - for tools that fetch untrusted URLs, so a prompt-injected request cannot reach
    /// Sentium's own infrastructure or the local network.
    /// </summary>
    PublicOnly
}
