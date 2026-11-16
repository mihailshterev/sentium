using System.Text;

namespace Sentium.Sandbox.Docker;

/// <summary>
/// A write-only stream that keeps the first <c>capacity</c> bytes of a container's output and counts (but drops) the
/// rest, so a program that prints in a loop cannot grow the Sandbox's memory until the execution timeout.
/// </summary>
internal sealed class BoundedOutputBuffer(int capacity) : Stream
{
    private readonly MemoryStream _kept = new();

    public long TotalBytes { get; private set; }

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => TotalBytes;

    public override long Position
    {
        get => TotalBytes;
        set => throw new NotSupportedException();
    }

    /// <summary>
    /// The kept output as UTF-8 text, with a note when bytes were dropped.
    /// </summary>
    public string ToText(string streamName)
    {
        var text = Encoding.UTF8.GetString(_kept.GetBuffer(), 0, (int)_kept.Length);

        return TotalBytes > _kept.Length
            ? text + $"\n[{streamName} truncated at {capacity} bytes]"
            : text;
    }

    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        TotalBytes += buffer.Length;

        var room = capacity - (int)_kept.Length;
        if (room > 0)
        {
            _kept.Write(buffer[..Math.Min(room, buffer.Length)]);
        }
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        Write(buffer.AsSpan(offset, count));
        return Task.CompletedTask;
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Write(buffer.Span);
        return ValueTask.CompletedTask;
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _kept.Dispose();
        }

        base.Dispose(disposing);
    }
}
