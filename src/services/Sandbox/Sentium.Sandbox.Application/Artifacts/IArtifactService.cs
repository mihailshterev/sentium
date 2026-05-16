using Sentium.Sandbox.Core.Models;

namespace Sentium.Sandbox.Application.Artifacts;

/// <summary>
/// Scans a completed job directory for files created or modified during execution,
/// uploads them to blob storage, and returns a record for each artifact.
/// </summary>
/// <remarks>
/// Implementations must be resilient: a failure to upload one artifact must not
/// prevent the remaining artifacts from being processed.
/// </remarks>
public interface IArtifactService
{
    /// <summary>
    /// Harvests artifacts from <paramref name="jobDirectory"/>.
    /// </summary>
    /// <param name="jobDirectory">Absolute path to the completed job directory.</param>
    /// <param name="inputFileNames">
    /// Normalized relative paths of the files that were written as inputs
    /// (e.g. <c>main.py</c>, <c>data/input.csv</c>). These are excluded from harvesting.
    /// </param>
    /// <param name="jobId">Job identifier — used as the blob path prefix.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>One <see cref="ArtifactRecord"/> per successfully uploaded artifact.</returns>
    Task<IReadOnlyList<ArtifactRecord>> HarvestAsync(string jobDirectory, IReadOnlySet<string> inputFileNames, Guid jobId, CancellationToken ct);
}
