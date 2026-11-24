using Microsoft.EntityFrameworkCore;
using Sentium.Infrastructure.Retention;
using Sentium.Infrastructure.Storage;
using Sentium.Praxis.Data;
using Sentium.Praxis.Features.CodeExecution;

namespace Sentium.Praxis.Features.Retention;

/// <summary>
/// Deletes sandbox artifacts that no execution record points at: runs whose record failed to save, and runs expired
/// before expiry deleted their artifacts too.
/// </summary>
/// <remarks>
/// The Sandbox uploads a run's artifacts before Praxis records the run, so a run that is still in flight looks
/// orphaned. A run is therefore deleted only when two consecutive sweeps (a day apart) both found it orphaned; the
/// first sweep after a restart only takes note.
/// </remarks>
internal sealed class OrphanedArtifactSweep(
    PraxisDbContext db,
    IObjectStore objectStore,
    SandboxArtifactRemover artifacts,
    OrphanedArtifactSuspects suspects,
    ILogger<OrphanedArtifactSweep> logger) : IRetentionSweep
{
    private const int LookupBatchSize = 500;

    public async Task<int> SweepAsync(DateTimeOffset now, CancellationToken ct)
    {
        var stored = new HashSet<Guid>();
        await foreach (var key in objectStore.ListKeysAsync(artifacts.Bucket, ct: ct))
        {
            if (SandboxArtifactRemover.JobIdOf(key) is { } jobId)
            {
                stored.Add(jobId);
            }
        }

        var orphaned = await FindUnrecordedAsync(stored, ct);
        var confirmed = suspects.Confirm(orphaned);
        var deleted = await artifacts.DeleteAsync(confirmed, ct);

        if (deleted > 0)
        {
            logger.LogInformation("Deleted the orphaned artifacts of {Count} sandbox runs.", deleted);
        }

        return deleted;
    }

    private async Task<HashSet<Guid>> FindUnrecordedAsync(HashSet<Guid> jobIds, CancellationToken ct)
    {
        var orphaned = new HashSet<Guid>(jobIds);

        foreach (var batch in jobIds.Chunk(LookupBatchSize))
        {
            var recorded = await db.ExecutionLogs
                .IgnoreQueryFilters()
                .Where(e => batch.Contains(e.JobId))
                .Select(e => e.JobId)
                .ToListAsync(ct);

            orphaned.ExceptWith(recorded);
        }

        return orphaned;
    }
}
