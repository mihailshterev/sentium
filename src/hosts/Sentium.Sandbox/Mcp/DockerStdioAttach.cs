using System.IO.Pipes;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Options;
using Sentium.Sandbox.Docker;
using Sentium.Sandbox.Options;

namespace Sentium.Sandbox.Mcp;

/// <summary>
/// Attaches to a container's stdin/stdout/stderr the way the Docker CLI does: <c>POST /containers/{id}/attach</c> with
/// <c>Connection: Upgrade</c> / <c>Upgrade: tcp</c>, after which the daemon hands over the raw connection. Docker.DotNet's
/// <c>AttachContainerAsync</c> is not hijacked (it returns a plain response stream), so writes to it never reach the
/// container's stdin - which a stdio MCP server needs.
/// </summary>
internal sealed class DockerStdioAttach(IOptions<SandboxOptions> options)
{
    private const int MaxResponseHeaderBytes = 16 * 1024;

    public async Task<IContainerStdio> AttachAsync(string containerId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerId);

        var endpoint = DockerEndpoint.Resolve(options.Value);
        var (stream, socket) = await ConnectAsync(endpoint, ct);

        try
        {
            var request =
                $"POST /containers/{Uri.EscapeDataString(containerId)}/attach?stream=1&stdin=1&stdout=1&stderr=1 HTTP/1.1\r\n" +
                "Host: docker\r\n" +
                "Connection: Upgrade\r\n" +
                "Upgrade: tcp\r\n" +
                "Content-Length: 0\r\n\r\n";

            await stream.WriteAsync(Encoding.ASCII.GetBytes(request), ct);
            await stream.FlushAsync(ct);

            var head = await ReadResponseHeadAsync(stream, ct);
            var statusLine = head.Split("\r\n", 2)[0];
            if (!statusLine.Contains(" 101 ", StringComparison.Ordinal) && !statusLine.Contains(" 200 ", StringComparison.Ordinal))
            {
                throw new IOException($"Docker refused to attach to container {containerId}: {statusLine}");
            }

            return new HijackedContainerStdio(stream, socket);
        }
        catch
        {
            await stream.DisposeAsync();
            throw;
        }
    }

    private static async Task<(Stream Stream, Socket? Socket)> ConnectAsync(Uri endpoint, CancellationToken ct)
    {
        switch (endpoint.Scheme)
        {
            case "npipe":
            {
                // npipe://./pipe/docker_engine
                var pipeName = endpoint.AbsolutePath.StartsWith("/pipe/", StringComparison.Ordinal) ? endpoint.AbsolutePath["/pipe/".Length..] : endpoint.AbsolutePath.TrimStart('/');
                var server = string.IsNullOrEmpty(endpoint.Host) ? "." : endpoint.Host;
                var pipe = new NamedPipeClientStream(server, pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                try
                {
                    await pipe.ConnectAsync(ct);
                    return (pipe, null);
                }
                catch
                {
                    await pipe.DisposeAsync();
                    throw;
                }
            }

            case "unix":
            {
                var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                try
                {
                    await socket.ConnectAsync(new UnixDomainSocketEndPoint(endpoint.AbsolutePath), ct);
                    return (new NetworkStream(socket, ownsSocket: true), socket);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            }

            case "tcp" or "http":
            {
                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                try
                {
                    await socket.ConnectAsync(endpoint.Host, endpoint.Port, ct);
                    return (new NetworkStream(socket, ownsSocket: true), socket);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            }

            default:
                throw new NotSupportedException($"Docker endpoint scheme '{endpoint.Scheme}' is not supported for MCP stdio attach (TLS endpoints are not).");
        }
    }

    /// <summary>
    /// Reads the HTTP response head byte by byte, so nothing of the hijacked stream that follows is consumed.
    /// </summary>
    private static async Task<string> ReadResponseHeadAsync(Stream stream, CancellationToken ct)
    {
        var head = new List<byte>(256);
        var one = new byte[1];

        while (head.Count < MaxResponseHeaderBytes)
        {
            if (await stream.ReadAsync(one, ct) == 0)
            {
                throw new IOException("Docker closed the connection while attaching.");
            }

            head.Add(one[0]);
            if (head.Count >= 4 && head[^4] == '\r' && head[^3] == '\n' && head[^2] == '\r' && head[^1] == '\n')
            {
                return Encoding.ASCII.GetString([.. head]);
            }
        }

        throw new IOException("Docker's attach response header was too large.");
    }
}
