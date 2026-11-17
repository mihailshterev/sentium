using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sentium.Identity.Contracts;
using Sentium.Infrastructure.Security;
using Sentium.Infrastructure.Storage;
using Sentium.Praxis.Data;
using Sentium.Praxis.Features.CodeExecution;
using Sentium.Praxis.Features.Storage;

namespace Sentium.Praxis.Features.Users;

/// <summary>
/// Deletes a removed user's agents, skills, workflows and runs, conversations, workspaces and files (with their blobs),
/// learnings and sandbox history (with its artifacts). The user's knowledge chunks are Codex's to remove.
/// </summary>
/// <remarks>
/// The rows go first and the blobs after, one by one: an unreachable blob store then leaves orphaned blobs (logged),
/// never a user's records.
/// </remarks>
internal sealed class PraxisUserDataRemover(
    IServiceScopeFactory scopeFactory,
    IObjectStore objectStore,
    IOptions<SandboxArtifactOptions> artifactOptions,
    ILogger<PraxisUserDataRemover> logger) : IUserRemovalObserver
{
    public async Task OnUserRemovedAsync(Guid userId, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<SystemScopeContext>().Activate();
        var db = scope.ServiceProvider.GetRequiredService<PraxisDbContext>();

        var fileBlobs = await db.ProjectFiles.IgnoreQueryFilters()
            .Where(f => f.UserId == userId)
            .Select(f => f.BlobName)
            .ToListAsync(ct);

        var runs = await db.ExecutionLogs.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .ToListAsync(ct);

        var deleted = await DeleteRecordsAsync(db, userId, ct);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Deleted {Count} Praxis records of removed user {UserId}.", deleted, userId);
        }

        var files = scope.ServiceProvider.GetRequiredService<ILocalFileService>();
        foreach (var blobName in fileBlobs)
        {
            await DeleteBlobAsync(() => files.DeleteFileAsync(blobName, ct), blobName.ToString());
        }

        var artifactBucket = artifactOptions.Value.ArtifactContainerName;
        foreach (var artifact in runs.SelectMany(r => r.Artifacts))
        {
            var key = ObjectUri.GetKey(artifact.BlobUri, artifactBucket);
            if (key is null)
            {
                logger.LogWarning("Artifact {BlobUri} of a removed user is not in the {Bucket} bucket; it is orphaned.", artifact.BlobUri, artifactBucket);
                continue;
            }

            await DeleteBlobAsync(() => objectStore.DeleteAsync(artifactBucket, key, ct), key);
        }
    }

    private static async Task<int> DeleteRecordsAsync(PraxisDbContext db, Guid userId, CancellationToken ct)
    {
        return await db.WorkflowRuns.IgnoreQueryFilters().Where(r => r.UserId == userId).ExecuteDeleteAsync(ct)
            + await db.Workflows.IgnoreQueryFilters().Where(w => w.UserId == userId).ExecuteDeleteAsync(ct)
            + await db.Agents.IgnoreQueryFilters().Where(a => a.UserId == userId).ExecuteDeleteAsync(ct)
            + await db.AgentToolOverrides.IgnoreQueryFilters().Where(o => o.UserId == userId).ExecuteDeleteAsync(ct)
            + await db.AgentSkills.IgnoreQueryFilters().Where(s => s.UserId == userId).ExecuteDeleteAsync(ct)
            + await db.AgentLearnings.IgnoreQueryFilters().Where(l => l.UserId == userId).ExecuteDeleteAsync(ct)
            + await db.Conversations.IgnoreQueryFilters().Where(c => c.UserId == userId).ExecuteDeleteAsync(ct)
            + await db.ProjectFiles.IgnoreQueryFilters().Where(f => f.UserId == userId).ExecuteDeleteAsync(ct)
            + await db.Workspaces.IgnoreQueryFilters().Where(w => w.UserId == userId).ExecuteDeleteAsync(ct)
            + await db.ExecutionLogs.IgnoreQueryFilters().Where(e => e.UserId == userId).ExecuteDeleteAsync(ct);
    }

    private async Task DeleteBlobAsync(Func<Task> delete, string blobName)
    {
        try
        {
            await delete();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not delete blob {BlobName} of a removed user; it is orphaned.", blobName);
        }
    }
}
