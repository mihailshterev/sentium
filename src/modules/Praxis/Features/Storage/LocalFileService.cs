using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Sentium.Core.Constants;

namespace Sentium.Praxis.Features.Storage;

/// <summary>
/// Implements file storage operations using Azure Blob Storage as the backend.
/// Files are stored in the <see cref="Sentium.Core.Constants.ResourceNames.WorkspaceBlobs"/> container, one blob per
/// file named by a GUID, with the filename and workspace as blob metadata.
/// </summary>
internal sealed class LocalFileService(BlobServiceClient blobServiceClient) : ILocalFileService
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
