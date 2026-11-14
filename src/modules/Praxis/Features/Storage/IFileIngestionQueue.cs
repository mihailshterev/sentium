namespace Sentium.Praxis.Features.Storage;

/// <summary>
/// Hands newly stored files to <see cref="FileIngestionWorker"/> so uploads return without waiting for the
/// expensive vectorization step.
/// </summary>
public interface IFileIngestionQueue
{
    void Enqueue(FileIngestionRequest request);
}
