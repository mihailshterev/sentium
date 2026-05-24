using Sentium.AgentRuntime.Core.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Sentium.Shared.Constants;

namespace Sentium.AgentRuntime.Infrastructure.Storage;

/// <summary>
/// Implements file storage operations using Azure Blob Storage as the backend.
/// </summary>
/// <remarks>
/// <para>
/// This service handles the lifecycle of file blobs:
/// - Upload: Stores files with metadata (filename, workspace association).
/// - Retrieve: Streams file content back for ingestion or download.
/// - Delete: Removes files after processing or on user request.
/// </para>
/// <para>
/// Files are stored in a container named by <see cref="Sentium.Shared.Constants.ResourceNames.WorkspaceBlobs"/>.
/// Each blob is identified by a unique GUID and includes metadata for tracking.
/// </para>
/// </remarks>
public sealed class LocalFileService(BlobServiceClient blobServiceClient) : ILocalFileService
{
    /// <inheritdoc/>
    public async Task<Guid> UploadToWorkspaceAsync(Stream fileStream, string fileName, Guid? workspaceId, CancellationToken ct = default)
    {
        var blobContainerClient = blobServiceClient.GetBlobContainerClient(ResourceNames.WorkspaceBlobs);
        await blobContainerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);

        var blobName = Guid.NewGuid();
        var blobClient = blobContainerClient.GetBlobClient(blobName.ToString());

        var metadata = new Dictionary<string, string>
        {
            { "FileName", fileName }
        };

        if (workspaceId.HasValue)
        {
            metadata.Add("WorkspaceId", workspaceId.Value.ToString());
        }

        await blobClient.UploadAsync(fileStream, new BlobUploadOptions
        {
            Metadata = metadata
        }, ct);

        return blobName;
    }

    /// <inheritdoc/>
    public async Task<Stream> GetFileStreamAsync(Guid blobName, CancellationToken ct = default)
    {
        var blobContainerClient = blobServiceClient.GetBlobContainerClient(ResourceNames.WorkspaceBlobs);
        var blobClient = blobContainerClient.GetBlobClient(blobName.ToString());

        var response = await blobClient.DownloadStreamingAsync(cancellationToken: ct);
        return response.Value.Content;
    }

    /// <inheritdoc/>
    public async Task DeleteFileAsync(Guid blobName, CancellationToken ct = default)
    {
        var blobContainerClient = blobServiceClient.GetBlobContainerClient(ResourceNames.WorkspaceBlobs);
        var blobClient = blobContainerClient.GetBlobClient(blobName.ToString());

        await blobClient.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: ct);
    }
}
