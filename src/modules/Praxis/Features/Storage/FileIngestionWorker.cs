using Sentium.Codex.Contracts;
using Sentium.Codex.Contracts.Models;
using Sentium.Praxis.Data;
using Sentium.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Sentium.Praxis.Features.Files;

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
public sealed class FileIngestionWorker(IServiceScopeFactory scopeFactory, FileIngestionQueue queue, ILogger<FileIngestionWorker> logger) : BackgroundService
{
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
    /// 3. Reads file content from blob storage.
    /// 4. Calls the ingestion service to vectorize and index the content.
    /// 5. Updates status to Completed on success, or Failed on exception.
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
        var fileService = scope.ServiceProvider.GetRequiredService<ILocalFileService>();
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
            await dbContext.SaveChangesAsync(ct);

            using var stream = await fileService.GetFileStreamAsync(projectFile.BlobName, ct);
            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync(ct);

            var ingestionRequest = new IngestionRequest
            {
                Source = projectFile.FileName,
                SourceType = IngestionSourceType.File,
                Content = content,
                Metadata = new Dictionary<string, string>
                {
                    { "FileId", projectFile.Id.ToString() },
                    { "BlobName", projectFile.BlobName.ToString() },
                    { "Extension", projectFile.Extension }
                },
                Scope = KnowledgeScope.User,
                UserId = projectFile.UserId
            };

            if (workspaceId.HasValue)
            {
                ingestionRequest.Metadata.Add("WorkspaceId", workspaceId.Value.ToString());
            }

            await ingestionService.IngestAsync(ingestionRequest, ct: ct);

            projectFile.ProcessingStatus = FileProcessingStatus.Completed;
            logger.LogInformation("Successfully ingested file {FileName} ({FileId})", projectFile.FileName, fileId);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Failed to ingest file {FileName} ({FileId})", projectFile.FileName, fileId);
            projectFile.ProcessingStatus = FileProcessingStatus.Failed;
        }
        finally
        {
            await dbContext.SaveChangesAsync(ct);
        }
    }
}
