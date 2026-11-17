using Sentium.Infrastructure.Storage;

namespace Sentium.Locus.Features.Attachments;

/// <summary>
/// <see cref="IAttachmentStore"/> on the platform's object storage, in a private bucket owned by this module.
/// No user input ever reaches object keys or metadata.
/// </summary>
internal sealed class BlobAttachmentStore(IObjectStore objectStore) : IAttachmentStore
{
    public const string ContainerName = "locus-attachments";

    public Task UploadAsync(string blobName, Stream content, string contentType, CancellationToken ct = default)
    {
        return objectStore.PutAsync(ContainerName, blobName, content, contentType, ct: ct);
    }

    public async Task<Stream?> OpenReadAsync(string blobName, CancellationToken ct = default)
    {
        var stored = await objectStore.OpenReadAsync(ContainerName, blobName, ct);
        return stored?.Content;
    }

    public Task DeleteAsync(string blobName, CancellationToken ct = default)
    {
        return objectStore.DeleteAsync(ContainerName, blobName, ct);
    }

    public Task DeletePrefixAsync(string prefix, CancellationToken ct = default)
    {
        return objectStore.DeletePrefixAsync(ContainerName, prefix, ct);
    }
}
