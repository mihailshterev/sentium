using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Sentium.Locus.Features.Attachments;

/// <summary>
/// <see cref="IAttachmentStore"/> on the platform's blob storage (Azurite locally), in a private container
/// owned by this module. No user input ever reaches blob names or metadata.
/// </summary>
internal sealed class BlobAttachmentStore(BlobServiceClient blobServiceClient) : IAttachmentStore
{
    public const string ContainerName = "locus-attachments";

    public async Task UploadAsync(string blobName, Stream content, string contentType, CancellationToken ct = default)
    {
        var container = await GetContainerAsync(ct);

        await container.GetBlobClient(blobName).UploadAsync(content, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
        }, ct);
    }

    public async Task<Stream?> OpenReadAsync(string blobName, CancellationToken ct = default)
    {
        var blob = blobServiceClient.GetBlobContainerClient(ContainerName).GetBlobClient(blobName);

        try
        {
            return await blob.OpenReadAsync(new BlobOpenReadOptions(allowModifications: false), ct);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string blobName, CancellationToken ct = default)
    {
        var blob = blobServiceClient.GetBlobContainerClient(ContainerName).GetBlobClient(blobName);
        await blob.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: ct);
    }

    public async Task DeletePrefixAsync(string prefix, CancellationToken ct = default)
    {
        var container = blobServiceClient.GetBlobContainerClient(ContainerName);

        if (!await container.ExistsAsync(ct))
        {
            return;
        }

        await foreach (var item in container.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix, ct))
        {
            await container.DeleteBlobIfExistsAsync(item.Name, DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: ct);
        }
    }

    private async Task<BlobContainerClient> GetContainerAsync(CancellationToken ct)
    {
        var container = blobServiceClient.GetBlobContainerClient(ContainerName);
        await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);
        return container;
    }
}
