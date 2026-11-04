using System.Threading.Channels;

namespace Sentium.Praxis.Features.Storage;

/// <summary>
/// A stored file waiting for RAG ingestion (read from blob storage, vectorized, indexed).
/// </summary>
public sealed record FileIngestionRequest(Guid FileId, Guid? WorkspaceId);

/// <summary>
/// Hands newly stored files to <see cref="FileIngestionWorker"/> so uploads return without waiting for the
/// expensive vectorization step.
/// </summary>
public interface IFileIngestionQueue
{
    void Enqueue(FileIngestionRequest request);
}

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
