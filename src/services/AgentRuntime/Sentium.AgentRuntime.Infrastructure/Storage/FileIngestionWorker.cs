using Sentium.AgentRuntime.Core.Rag;
using Sentium.AgentRuntime.Core.Rag.Models;
using Sentium.AgentRuntime.Core.Storage;
using Sentium.AgentRuntime.Infrastructure.Data;
using Sentium.Infrastructure.Messaging;
using Sentium.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sentium.AgentRuntime.Core.Files;

namespace Sentium.AgentRuntime.Infrastructure.Storage;

/// <summary>
/// Background service that processes files asynchronously for RAG (Retrieval-Augmented Generation) ingestion.
/// </summary>
/// <remarks>
/// <para>
/// This worker:
/// 1. Subscribes to <see cref="FileIngestedEvent"/> events published when files are uploaded.
/// 2. Retrieves file content from blob storage.
/// 3. Vectorizes the content using a semantic embedding model.
/// 4. Stores the vectors in a RAG index (e.g., Qdrant).
/// 5. Updates the <see cref="Core.Entities.ProjectFile.ProcessingStatus"/> to Completed or Failed.
/// </para>
/// <para>
/// This asynchronous processing ensures that file uploads complete quickly without waiting for
/// expensive vectorization operations. If processing fails, the status is marked as Failed for
/// later inspection or retry.
/// </para>
/// </remarks>
public sealed class FileIngestionWorker(IServiceScopeFactory scopeFactory, IEventBus eventBus, ILogger<FileIngestionWorker> logger) : BackgroundService
{
    /// <summary>
    /// Starts the worker and subscribes to file ingestion events.
    /// </summary>
    /// <param name="stoppingToken">A cancellation token to signal shutdown.</param>
    /// <remarks>
    /// This method runs indefinitely, listening for <see cref="FileEvents.FileIngested"/> events
    /// from the event bus and processing files as they arrive.
    /// </remarks>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("File Ingestion Worker started.");

        await eventBus.SubscribeAsync<FileIngestedEvent>(FileEvents.FileIngested, async msg =>
        {
            var evt = msg.Data!;
            await ProcessFileAsync(evt.FileId, evt.WorkspaceId, stoppingToken);
        }, stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    /// <summary>
    /// Processes a single file for RAG ingestion.
    /// </summary>
    /// <param name="fileId">The unique identifier of the file to process.</param>
    /// <param name="workspaceId">The optional workspace ID associated with the file.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <remarks>
    /// <para>
    /// Processing flow:
    /// 1. Fetches the file metadata from the database.
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
        var dbContext = scope.ServiceProvider.GetRequiredService<AgentRuntimeDbContext>();
        var fileService = scope.ServiceProvider.GetRequiredService<ILocalFileService>();
        var ingestionService = scope.ServiceProvider.GetRequiredService<IDocumentIngestionService>();

        var projectFile = await dbContext.ProjectFiles.FindAsync([fileId], ct);
        if (projectFile is null)
        {
            logger.LogWarning("Project file {FileId} not found in database.", fileId);
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
        catch (Exception ex)
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
