using System.Threading.Channels;

namespace Sentium.Praxis.Features.Storage;

/// <summary>
/// In-memory FIFO. Nothing is lost on restart: the file's <c>ProcessingStatus</c> in the database is the
/// source of truth, and the worker re-queues every unfinished file when it starts.
/// </summary>
public sealed class FileIngestionQueue : IFileIngestionQueue
{
    private readonly Channel<FileIngestionRequest> _channel = Channel.CreateUnbounded<FileIngestionRequest>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    public ChannelReader<FileIngestionRequest> Reader => _channel.Reader;

    public void Enqueue(FileIngestionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        _channel.Writer.TryWrite(request);
    }
}
