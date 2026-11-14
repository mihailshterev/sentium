namespace Sentium.Locus.Features.Attachments;

/// <summary>
/// Binary storage for asset attachments. Blob names are always <c>{assetId}/{attachmentId}</c>.
/// </summary>
internal interface IAttachmentStore
{
    Task UploadAsync(string blobName, Stream content, string contentType, CancellationToken ct = default);

    /// <summary>
    /// Opens the blob for reading, or returns <see langword="null"/> when it does not exist.
    /// </summary>
    Task<Stream?> OpenReadAsync(string blobName, CancellationToken ct = default);

    Task DeleteAsync(string blobName, CancellationToken ct = default);

    /// <summary>
    /// Deletes every blob whose name starts with <paramref name="prefix"/> (all of an asset's files).
    /// </summary>
    Task DeletePrefixAsync(string prefix, CancellationToken ct = default);
}
