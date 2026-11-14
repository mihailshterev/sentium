using System.Buffers.Binary;
using System.Net.Sockets;

namespace Sentium.Sandbox.Mcp;

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
