using System.Buffers;
using System.IO.Pipelines;
using System.Text;

namespace Sentium.Sandbox.Mcp;

/// <summary>
/// Turns a container's stdio into the two plain streams the MCP SDK's <c>StreamClientTransport</c> expects:
/// <see cref="ServerInput"/> (what the client writes - the container's stdin) and <see cref="ServerOutput"/> (what it
/// reads - the container's stdout). A pump demultiplexes the attach stream, keeps a bounded tail of stderr for
/// diagnostics, and fails the session if a single message grows beyond the configured cap (a server that never emits a
/// newline would otherwise grow memory without bound).
/// </summary>
internal sealed class ContainerStdioBridge : IAsyncDisposable
{
    private const int BufferSize = 64 * 1024;

    private readonly IContainerStdio stdio;
    private readonly int maxMessageBytes;
    private readonly int stderrLines;
    private readonly Pipe stdout = new();
    private readonly CancellationTokenSource stop = new();
    private readonly Lock stderrLock = new();
    private readonly LinkedList<string> stderrTail = new();
    private readonly StringBuilder stderrLine = new();
    private readonly Task pump;

    public ContainerStdioBridge(IContainerStdio stdio, int maxMessageBytes, int stderrLines)
    {
        ArgumentNullException.ThrowIfNull(stdio);

        this.stdio = stdio;
        this.maxMessageBytes = maxMessageBytes;
        this.stderrLines = stderrLines;

        ServerInput = new StdinStream(stdio);
        ServerOutput = stdout.Reader.AsStream();
        pump = Task.Run(() => PumpAsync(stop.Token));
    }

    public Stream ServerInput { get; }

    public Stream ServerOutput { get; }

    /// <summary>
    /// Completes when the container's output ended (process exit) or the bridge failed.
    /// </summary>
    public Task Completion => pump;

    public IReadOnlyList<string> StderrTail
    {
        get
        {
            lock (stderrLock)
            {
                return [.. stderrTail];
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await stop.CancelAsync();
        try
        {
            ServerInput.Dispose();
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            // The container may already be gone.
        }

        stdio.Dispose();

        try
        {
            await pump;
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or ObjectDisposedException)
        {
            // Expected while tearing down.
        }

        await ServerOutput.DisposeAsync();
        stop.Dispose();
    }

    private async Task PumpAsync(CancellationToken ct)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        var bytesSinceNewline = 0L;
        Exception? failure = null;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var chunk = await stdio.ReadAsync(buffer, ct);
                if (chunk.EndOfStream || chunk.Count == 0)
                {
                    break;
                }

                if (chunk.Target == StdioTarget.Stderr)
                {
                    AppendStderr(buffer.AsSpan(0, chunk.Count));
                    continue;
                }

                var data = buffer.AsMemory(0, chunk.Count);
                var lastNewline = data.Span.LastIndexOf((byte)'\n');
                bytesSinceNewline = lastNewline >= 0 ? chunk.Count - lastNewline - 1 : bytesSinceNewline + chunk.Count;

                if (bytesSinceNewline > maxMessageBytes)
                {
                    throw new InvalidDataException($"The MCP server wrote a message larger than {maxMessageBytes} bytes.");
                }

                var flush = await stdout.Writer.WriteAsync(data, ct);
                if (flush.IsCompleted)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Disposed.
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ObjectDisposedException)
        {
            failure = ex;
            AppendStderr(Encoding.UTF8.GetBytes($"[sentium] stdio bridge failed: {ex.Message}\n"));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            await stdout.Writer.CompleteAsync(failure);
        }
    }

    private void AppendStderr(ReadOnlySpan<byte> bytes)
    {
        var text = Encoding.UTF8.GetString(bytes);

        lock (stderrLock)
        {
            foreach (var c in text)
            {
                if (c == '\n')
                {
                    PushStderrLine();
                }
                else if (c != '\r' && stderrLine.Length < 2_000)
                {
                    stderrLine.Append(c);
                }
            }
        }
    }

    private void PushStderrLine()
    {
        stderrTail.AddLast(stderrLine.ToString());
        stderrLine.Clear();

        while (stderrTail.Count > stderrLines)
        {
            stderrTail.RemoveFirst();
        }
    }

    /// <summary>
    /// Write-only stream onto the container's stdin; writes are serialized and go straight through.
    /// </summary>
    private sealed class StdinStream(IContainerStdio stdio) : Stream
    {
        private readonly SemaphoreSlim writeLock = new(1, 1);
        private bool closed;

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => !closed;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(closed, this);

            var rented = ArrayPool<byte>.Shared.Rent(buffer.Length);
            try
            {
                buffer.CopyTo(rented);

                await writeLock.WaitAsync(cancellationToken);
                try
                {
                    await stdio.WriteAsync(rented, 0, buffer.Length, cancellationToken);
                }
                finally
                {
                    writeLock.Release();
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override void Write(byte[] buffer, int offset, int count) =>
            WriteAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();

        public override void Flush()
        {
        }

        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing && !closed)
            {
                closed = true;
                stdio.CloseInput();
                writeLock.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
