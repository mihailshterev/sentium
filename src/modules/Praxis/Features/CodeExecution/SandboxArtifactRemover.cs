using Microsoft.Extensions.Options;
using Sentium.Infrastructure.Storage;

namespace Sentium.Praxis.Features.CodeExecution;

/// <summary>
/// Deletes sandbox runs' artifacts from object storage. The Sandbox stores every artifact of a run under the key prefix
/// <c>{JobId:N}/</c>, so a run's artifacts go together whether or not its record still lists them. A failed delete is
/// logged and the run's blobs stay orphaned until the orphan sweep finds them again.
/// </summary>
internal sealed class SandboxArtifactRemover(
    IObjectStore objectStore,
    IOptions<SandboxArtifactOptions> options,
    ILogger<SandboxArtifactRemover> logger)
{
    public string Bucket => options.Value.ArtifactContainerName;

    public static string PrefixOf(Guid jobId) => $"{jobId:N}/";

    /// <summary>
    /// The run a key belongs to, or <see langword="null"/> for a key outside the <c>{JobId:N}/</c> layout.
    /// </summary>
    public static Guid? JobIdOf(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        var separator = key.IndexOf('/', StringComparison.Ordinal);
        return separator > 0 && Guid.TryParseExact(key.AsSpan(0, separator), "N", out var jobId) ? jobId : null;
    }

    /// <returns>The number of runs whose artifacts were deleted.</returns>
    public async Task<int> DeleteAsync(IEnumerable<Guid> jobIds, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(jobIds);

        var deleted = 0;
        foreach (var jobId in jobIds)
        {
            try
            {
                await objectStore.DeletePrefixAsync(Bucket, PrefixOf(jobId), ct);
                deleted++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Could not delete the artifacts of sandbox run {JobId}; they stay orphaned for now.", jobId);
            }
        }

        return deleted;
    }
}
