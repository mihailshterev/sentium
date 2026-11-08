using System.Buffers.Binary;
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

/// <summary>
/// A hijacked attach connection. Docker multiplexes stdout and stderr on it (non-TTY containers) as frames with an
/// 8-byte header - <c>[stream, 0, 0, 0, size (uint32 big-endian)]</c> - and takes raw bytes for stdin.
/// </summary>
internal sealed class HijackedContainerStdio(Stream stream, Socket? socket) : IContainerStdio
{
    private const byte StderrStream = 2;

    private readonly byte[] header = new byte[8];
    private int frameRemaining;
    private StdioTarget frameTarget;

    public async Task<StdioChunk> ReadAsync(byte[] buffer, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        while (frameRemaining == 0)
        {
            var read = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);
            if (read < header.Length)
            {
                return new StdioChunk(0, StdioTarget.Stdout, EndOfStream: true);
            }

            frameTarget = header[0] == StderrStream ? StdioTarget.Stderr : StdioTarget.Stdout;
            frameRemaining = (int)BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(4));
        }

        var count = await stream.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, frameRemaining)), ct);
        if (count == 0)
        {
            return new StdioChunk(0, frameTarget, EndOfStream: true);
        }

        frameRemaining -= count;
        return new StdioChunk(count, frameTarget, EndOfStream: false);
    }

    public async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct)
    {
        await stream.WriteAsync(buffer.AsMemory(offset, count), ct);
        await stream.FlushAsync(ct);
    }

    public void CloseInput()
    {
        try
        {
            // Sockets can half-close (EOF on the server's stdin); a named pipe cannot - removing the container ends it.
            socket?.Shutdown(SocketShutdown.Send);
        }
        catch (Exception ex) when (ex is SocketException or ObjectDisposedException)
        {
            // Already closed.
        }
    }

    public void Dispose() => stream.Dispose();
}
