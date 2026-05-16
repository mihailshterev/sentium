using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sentium.Sandbox.Application.Artifacts;
using Sentium.Sandbox.Application.Options;
using Sentium.Sandbox.Core.Models;

namespace Sentium.Sandbox.Infrastructure.Artifacts;

/// <summary>
/// Harvests output artifacts from a completed job directory and uploads them to Azure Blob Storage.
/// <para>
/// Blob path layout: <c>{container}/{jobId:N}/{relativeFilePath}</c>
/// </para>
/// </summary>
internal sealed class ArtifactService(
    BlobServiceClient blobServiceClient,
    IOptions<SandboxOptions> options,
    ILogger<ArtifactService> logger) : IArtifactService
{
    private static readonly Dictionary<string, string> MimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".json"] = "application/json",
        [".csv"] = "text/csv",
        [".tsv"] = "text/tab-separated-values",
        [".txt"] = "text/plain",
        [".log"] = "text/plain",
        [".md"] = "text/markdown",
        [".html"] = "text/html",
        [".xml"] = "application/xml",
        [".yaml"] = "application/yaml",
        [".yml"] = "application/yaml",
        [".pdf"] = "application/pdf",
        [".zip"] = "application/zip",
        [".gz"] = "application/gzip",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".svg"] = "image/svg+xml",
        [".py"] = "text/x-python",
        [".js"] = "text/javascript",
        [".ts"] = "text/typescript",
        [".parquet"] = "application/vnd.apache.parquet",
        [".npy"] = "application/octet-stream",
        [".pkl"] = "application/octet-stream",
    };

    /// <inheritdoc />
    public async Task<IReadOnlyList<ArtifactRecord>> HarvestAsync(
        string jobDirectory,
        IReadOnlySet<string> inputFileNames,
        Guid jobId,
        CancellationToken ct)
    {
        var opts = options.Value;

        if (!Directory.Exists(jobDirectory))
        {
            logger.LogWarning("Job directory {JobDirectory} does not exist; skipping artifact harvest.", jobDirectory);
            return [];
        }

        var container = blobServiceClient.GetBlobContainerClient(opts.ArtifactContainerName);
        await container.CreateIfNotExistsAsync(cancellationToken: ct);

        var allFiles = Directory.EnumerateFiles(jobDirectory, "*", SearchOption.AllDirectories);
        var artifacts = new List<ArtifactRecord>();

        foreach (var absolutePath in allFiles)
        {
            ct.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(jobDirectory, absolutePath).Replace('\\', '/');

            if (inputFileNames.Contains(relativePath))
            {
                continue;
            }

            var fileInfo = new FileInfo(absolutePath);

            if (fileInfo.Length > opts.MaxArtifactSizeBytes)
            {
                logger.LogWarning("Artifact '{RelativePath}' ({SizeBytes:N0} bytes) exceeds the limit of {LimitBytes:N0} bytes. Skipping.", relativePath, fileInfo.Length, opts.MaxArtifactSizeBytes);
                continue;
            }

            var mimeType = ResolveMimeType(fileInfo.Extension);
            var blobName = $"{jobId:N}/{relativePath}";

            try
            {
                var blobClient = container.GetBlobClient(blobName);

                await using var fileStream = File.OpenRead(absolutePath);

                await blobClient.UploadAsync(fileStream, new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders { ContentType = mimeType }
                }, ct);

                artifacts.Add(new ArtifactRecord
                {
                    FileName = relativePath,
                    MimeType = mimeType,
                    BlobUri = blobClient.Uri,
                    SizeBytes = fileInfo.Length
                });

                logger.LogInformation("Harvested artifact '{RelativePath}' ({SizeBytes:N0} bytes) → {BlobUri}", relativePath, fileInfo.Length, blobClient.Uri);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to upload artifact '{RelativePath}' for job {JobId}. Continuing with remaining artifacts.", relativePath, jobId);
            }
        }

        logger.LogInformation("Artifact harvest complete for job {JobId}. {Count} artifact(s) uploaded.", jobId, artifacts.Count);

        return artifacts;
    }

    private static string ResolveMimeType(string extension) => MimeTypes.TryGetValue(extension, out var mime) ? mime : "application/octet-stream";
}
