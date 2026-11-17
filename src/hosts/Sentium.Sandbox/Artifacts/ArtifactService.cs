using Microsoft.Extensions.Options;
using Sentium.Infrastructure.Storage;
using Sentium.Sandbox.Options;
using Sentium.Sandbox.Models;

namespace Sentium.Sandbox.Artifacts;

/// <summary>
/// Harvests output artifacts from a completed job directory and uploads them to object storage, as
/// <c>{bucket}/{jobId:N}/{relativeFilePath}</c>. Each artifact is referenced by its <see cref="ObjectUri"/>.
/// </summary>
internal sealed class ArtifactService(
    IObjectStore objectStore,
    IOptions<SandboxOptions> options,
    ILogger<ArtifactService> logger) : IArtifactService
{
    /// <summary>
    /// The job directory is writable by the sandboxed code, which could plant a symbolic link to a file of the
    /// Sandbox host itself (<c>/proc/1/environ</c> holds the internal API key). Links are never followed: neither
    /// into linked directories nor to read a linked file.
    /// </summary>
    private static readonly EnumerationOptions HarvestEnumeration = new()
    {
        RecurseSubdirectories = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
        IgnoreInaccessible = true,
    };

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

        var allFiles = Directory.EnumerateFiles(jobDirectory, "*", HarvestEnumeration);
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

            if (fileInfo.LinkTarget is not null)
            {
                logger.LogWarning("Artifact '{RelativePath}' of job {JobId} is a link; skipping it.", relativePath, jobId);
                continue;
            }

            if (fileInfo.Length > opts.MaxArtifactSizeBytes)
            {
                logger.LogWarning("Artifact '{RelativePath}' ({SizeBytes:N0} bytes) exceeds the limit of {LimitBytes:N0} bytes. Skipping.", relativePath, fileInfo.Length, opts.MaxArtifactSizeBytes);
                continue;
            }

            var mimeType = ResolveMimeType(fileInfo.Extension);
            var blobName = $"{jobId:N}/{relativePath}";

            try
            {
                await using var fileStream = File.OpenRead(absolutePath);

                await objectStore.PutAsync(opts.ArtifactContainerName, blobName, fileStream, mimeType, ct: ct);

                var blobUri = ObjectUri.Create(opts.ArtifactContainerName, blobName);
                artifacts.Add(new ArtifactRecord
                {
                    FileName = relativePath,
                    MimeType = mimeType,
                    BlobUri = blobUri,
                    Key = blobName,
                    SizeBytes = fileInfo.Length
                });

                logger.LogInformation("Harvested artifact '{RelativePath}' ({SizeBytes:N0} bytes) → {BlobUri}", relativePath, fileInfo.Length, blobUri);
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
