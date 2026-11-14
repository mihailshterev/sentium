using System.Net;
using System.Net.Sockets;

namespace Sentium.Infrastructure.Net;

/// <summary>
/// Enforces a <see cref="NetworkAddressScope"/> at socket-connect time. The check runs on the addresses the connection
/// actually uses (DNS is resolved here), so it cannot be bypassed by DNS rebinding or by a hostname that resolves
/// somewhere else than it looks like it should. Link-local addresses (169.254.0.0/16 - cloud metadata - and fe80::/10)
/// are allowed by neither scope.
/// </summary>
public static class NetworkAddressPolicy
{
    /// <summary>
    /// Resolves a host name to addresses; replaceable in tests.
    /// </summary>
    public delegate Task<IPAddress[]> HostResolver(string host, CancellationToken cancellationToken);

    private static readonly HostResolver DnsResolver = Dns.GetHostAddressesAsync;

    /// <summary>
    /// Whether <paramref name="address"/> may be connected to under <paramref name="scope"/>.
    /// </summary>
    public static bool IsAllowed(IPAddress address, NetworkAddressScope scope)
    {
        ArgumentNullException.ThrowIfNull(address);

        var normalized = Normalize(address);
        if (IsLinkLocal(normalized) || IsUnusable(normalized))
        {
            return false;
        }

        return scope switch
        {
            NetworkAddressScope.PrivateOnly => IsPrivate(normalized),
            NetworkAddressScope.PublicOnly => !IsPrivate(normalized),
            _ => false
        };
    }

    /// <summary>
    /// Loopback, RFC 1918 (10/8, 172.16/12, 192.168/16), RFC 6598 shared address space (100.64/10 - CGNAT, Tailscale)
    /// and IPv6 unique-local (fc00::/7) addresses. IPv4-mapped IPv6 addresses are judged by their IPv4 form.
    /// </summary>
    public static bool IsPrivate(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        var normalized = Normalize(address);
        if (IPAddress.IsLoopback(normalized))
        {
            return true;
        }

        if (normalized.AddressFamily == AddressFamily.InterNetwork)
        {
            Span<byte> b = stackalloc byte[4];
            normalized.TryWriteBytes(b, out _);

            return b[0] == 10
                || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                || (b[0] == 192 && b[1] == 168)
                || (b[0] == 100 && b[1] >= 64 && b[1] <= 127);
        }

        if (normalized.AddressFamily == AddressFamily.InterNetworkV6)
        {
            Span<byte> b = stackalloc byte[16];
            normalized.TryWriteBytes(b, out _);
            return (b[0] & 0xFE) == 0xFC;
        }

        return false;
    }

    /// <summary>
    /// A <see cref="SocketsHttpHandler.ConnectCallback"/> that resolves the target host itself and connects only to
    /// addresses allowed by <paramref name="scope"/>; throws <see cref="NetworkAddressBlockedException"/> otherwise.
    /// </summary>
    public static Func<SocketsHttpConnectionContext, CancellationToken, ValueTask<Stream>> CreateConnectCallback(
        NetworkAddressScope scope,
        HostResolver? resolver = null)
    {
        var resolve = resolver ?? DnsResolver;
        return (context, cancellationToken) => ConnectAsync(context, scope, resolve, cancellationToken);
    }

    /// <summary>
    /// A primary handler enforcing <paramref name="scope"/>: guarded connects, no proxy (a proxy would make the
    /// connect-time check meaningless) and no automatic redirects (a redirect must not escape the scope via a
    /// different host).
    /// </summary>
    public static SocketsHttpHandler CreateHandler(NetworkAddressScope scope, HostResolver? resolver = null)
    {
        return new()
        {
            ConnectCallback = CreateConnectCallback(scope, resolver),
            UseProxy = false,
            AllowAutoRedirect = false,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        };
    }

    private static async ValueTask<Stream> ConnectAsync(
        SocketsHttpConnectionContext context,
        NetworkAddressScope scope,
        HostResolver resolve,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var host = context.DnsEndPoint.Host;
        var port = context.DnsEndPoint.Port;

        var candidates = IPAddress.TryParse(host.Trim('[', ']'), out var literal)
            ? [literal]
            : await resolve(host, cancellationToken).ConfigureAwait(false);

        var allowed = candidates.Where(a => IsAllowed(a, scope)).ToArray();
        if (allowed.Length == 0)
        {
            throw new NetworkAddressBlockedException(host, scope);
        }

        SocketException? lastError = null;
        foreach (var address in allowed)
        {
            Socket? socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                socket.NoDelay = true;
                await socket.ConnectAsync(new IPEndPoint(address, port), cancellationToken).ConfigureAwait(false);
                var stream = new NetworkStream(socket, ownsSocket: true);
                socket = null;
                return stream;
            }
            catch (SocketException ex)
            {
                lastError = ex;
            }
            finally
            {
                socket?.Dispose();
            }
        }

        throw new HttpRequestException($"Could not connect to '{host}:{port}'.", lastError);
    }

    private static IPAddress Normalize(IPAddress address) => address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

    private static bool IsLinkLocal(IPAddress address)
    {
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            Span<byte> b = stackalloc byte[4];
            address.TryWriteBytes(b, out _);
            return b[0] == 169 && b[1] == 254;
        }

        return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal;
    }

    private static bool IsUnusable(IPAddress address)
    {
        if (address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any) || address.Equals(IPAddress.Broadcast) || address.IsIPv6Multicast)
        {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            Span<byte> b = stackalloc byte[4];
            address.TryWriteBytes(b, out _);
            return b[0] == 0 || b[0] >= 224;
        }

        return false;
    }
}
