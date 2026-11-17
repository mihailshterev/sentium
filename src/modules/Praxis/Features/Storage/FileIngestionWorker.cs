using Microsoft.EntityFrameworkCore;
using Sentium.Codex.Contracts;
using Sentium.Codex.Contracts.Models;
using Sentium.Infrastructure.Caching;
using Sentium.Infrastructure.Security;
using Sentium.Praxis.Data;
using Sentium.Praxis.Features.Files;
using Sentium.Praxis.Features.Workspaces;

namespace Sentium.Praxis.Features.Storage;

/// <summary>
/// Background service that processes files asynchronously for RAG (Retrieval-Augmented Generation) ingestion.
/// </summary>
/// <remarks>
/// <para>
/// This worker:
/// 1. Reads <see cref="FileIngestionRequest"/>s queued when files are stored (see <see cref="IFileIngestionQueue"/>).
/// 2. Retrieves file content from blob storage.
/// 3. Vectorizes the content using a semantic embedding model.
/// 4. Stores the vectors in the Codex vector store (pgvector).
/// 5. Updates the <see cref="Entities.ProjectFile.ProcessingStatus"/> to Completed or Failed.
/// </para>
/// <para>
/// This asynchronous processing ensures that file uploads complete quickly without waiting for
/// expensive vectorization operations. If processing fails, the status is marked as Failed for
/// later inspection or retry. Files still Pending or Processing when the application stopped are
/// re-queued on startup, so a restart never strands a file.
/// </para>
/// </remarks>
internal sealed class FileIngestionWorker(IServiceScopeFactory scopeFactory, FileIngestionQueue queue, ILogger<FileIngestionWorker> logger) : BackgroundService
{
    private const int MaxErrorLength = 1000;

    /// <summary>
    /// Re-queues unfinished files, then processes queued files one at a time until shutdown.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("File Ingestion Worker started.");

        try
        {
            await RequeueUnfinishedFilesAsync(stoppingToken);

            await foreach (var request in queue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await ProcessFileAsync(request.FileId, request.WorkspaceId, stoppingToken);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    logger.LogError(ex, "Unexpected error ingesting file {FileId}.", request.FileId);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("File Ingestion Worker is shutting down.");
        }
    }

    private async Task RequeueUnfinishedFilesAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        scope.ServiceProvider.GetRequiredService<SystemScopeContext>().Activate();
        var dbContext = scope.ServiceProvider.GetRequiredService<PraxisDbContext>();

        var unfinished = await dbContext.ProjectFiles
            .Where(f => f.ProcessingStatus == FileProcessingStatus.Pending || f.ProcessingStatus == FileProcessingStatus.Processing)
            .Select(f => new FileIngestionRequest(f.Id, f.WorkspaceId))
            .ToListAsync(ct);

        foreach (var request in unfinished)
        {
            queue.Enqueue(request);
        }

        if (unfinished.Count > 0)
        {
            logger.LogInformation("Re-queued {Count} file(s) left unfinished by a previous run.", unfinished.Count);
        }
    }

    /// <summary>
    /// Processes a single file for RAG ingestion.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Processing flow:
    /// 1. Fetches the file metadata from the database (skipping files already ingested).
    /// 2. Updates status to Processing.
    /// 3. Reads the file from blob storage and extracts its text (see <see cref="IWorkspaceFileTextReader"/>).
    /// 4. Calls the ingestion service to vectorize and index the content.
    /// 5. Updates status to Completed on success, or Failed (with the error) on exception.
    /// </para>
    /// <para>
    /// All exceptions are caught and logged; processing status is updated accordingly.
    /// </para>
    /// </remarks>
    private async Task ProcessFileAsync(Guid fileId, Guid? workspaceId, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        scope.ServiceProvider.GetRequiredService<SystemScopeContext>().Activate();
        var dbContext = scope.ServiceProvider.GetRequiredService<PraxisDbContext>();
        var textReader = scope.ServiceProvider.GetRequiredService<IWorkspaceFileTextReader>();
        var ingestionService = scope.ServiceProvider.GetRequiredService<IDocumentIngestionService>();

        var projectFile = await dbContext.ProjectFiles.FindAsync([fileId], ct);
        if (projectFile is null)
        {
            logger.LogWarning("Project file {FileId} not found in database.", fileId);
            return;
        }

        if (projectFile.ProcessingStatus == FileProcessingStatus.Completed)
        {
            return;
        }

        try
        {
            projectFile.ProcessingStatus = FileProcessingStatus.Processing;
            projectFile.ProcessingError = null;
            await dbContext.SaveChangesAsync(ct);

            var content = await textReader.ReadAsync(projectFile.BlobName, projectFile.Extension, ct);

            var ingestionRequest = new IngestionRequest
            {
                Source = projectFile.FileName,
                SourceType = IngestionSourceType.File,
                Content = content,
                Metadata = new Dictionary<string, string>
                {
                    { WorkspaceFileMetadata.FileId, projectFile.Id.ToString() },
                    { WorkspaceFileMetadata.BlobName, projectFile.BlobName.ToString() },
                    { WorkspaceFileMetadata.Extension, projectFile.Extension }
                },
                Scope = KnowledgeScope.User,
                UserId = projectFile.UserId
            };

            if (workspaceId.HasValue)
            {
                ingestionRequest.Metadata.Add(WorkspaceFileMetadata.WorkspaceId, workspaceId.Value.ToString());
            }

            // Replaces whatever an interrupted earlier attempt stored, so a retry never duplicates chunks.
            await ingestionService.ReplaceAsync(ingestionRequest, WorkspaceFileMetadata.FileId, ct: ct);

            if (!await dbContext.ProjectFiles.AnyAsync(f => f.Id == fileId, ct))
            {
                // Deleted while being ingested: the delete removed the chunks before this run stored them.
                await ingestionService.RemoveDocumentAsync(WorkspaceFileMetadata.FileId, fileId.ToString(), ct: ct);
                logger.LogInformation("File {FileId} was deleted during ingestion; its chunks were removed.", fileId);
                return;
            }

            projectFile.ProcessingStatus = FileProcessingStatus.Completed;
            logger.LogInformation("Successfully ingested file {FileName} ({FileId})", projectFile.FileName, fileId);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Failed to ingest file {FileName} ({FileId})", projectFile.FileName, fileId);
            projectFile.ProcessingStatus = FileProcessingStatus.Failed;
            projectFile.ProcessingError = Truncate(ex.Message);
        }

        await SaveStatusAsync(scope, dbContext, fileId, ct);
    }

    private async Task SaveStatusAsync(IServiceScope scope, PraxisDbContext dbContext, Guid fileId, CancellationToken ct)
    {
        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            logger.LogInformation("File {FileId} was deleted during ingestion; its status was not saved.", fileId);
        }

        await scope.ServiceProvider.GetRequiredService<IScopedCache>().InvalidateTagAsync(WorkspaceService.CacheTag, ct);
    }

    private static string Truncate(string message)
    {
        return message.Length <= MaxErrorLength ? message : message[..MaxErrorLength];
    }
}
