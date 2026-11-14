namespace Sentium.Infrastructure.Net;

/// <summary>
/// Thrown when an outbound connection targets an address outside the client's <see cref="NetworkAddressScope"/>.
/// </summary>
public sealed class NetworkAddressBlockedException : HttpRequestException
{
    public NetworkAddressBlockedException()
    {
    }

    public NetworkAddressBlockedException(string message) : base(message)
    {
    }

    public NetworkAddressBlockedException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public NetworkAddressBlockedException(string host, NetworkAddressScope scope)
        : base(scope == NetworkAddressScope.PrivateOnly
            ? $"Connection to '{host}' was blocked: only local-network addresses (loopback, 10/8, 172.16/12, 192.168/16, 100.64/10, fc00::/7) are allowed."
            : $"Connection to '{host}' was blocked: local-network and link-local addresses are not allowed.")
    {
        Host = host;
        Scope = scope;
    }

    public string? Host { get; }

    public NetworkAddressScope? Scope { get; }
}
