namespace Sentium.Praxis.Features.Storage;

/// <summary>
/// Abstraction over the object storage that holds workspace file content.
/// </summary>
internal interface ILocalFileService
{
    /// <summary>
    /// Uploads a file stream to workspace blob storage with associated metadata.
    /// </summary>
    /// <returns>A new GUID identifying the blob, to be stored as the file's <see cref="Entities.ProjectFile.BlobName"/>.</returns>
    Task<Guid> UploadToWorkspaceAsync(Stream fileStream, string fileName, Guid? workspaceId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves the content stream of a stored file by its blob identifier.
    /// </summary>
    /// <exception cref="FileNotFoundException">Thrown if the blob does not exist.</exception>
    Task<Stream> GetFileStreamAsync(Guid blobName, CancellationToken ct = default);

    /// <summary>
    /// Deletes a file from blob storage by its identifier. Idempotent: safe to call when the blob is absent.
    /// </summary>
    Task DeleteFileAsync(Guid blobName, CancellationToken ct = default);
}
