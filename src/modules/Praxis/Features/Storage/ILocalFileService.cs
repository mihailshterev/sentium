namespace Sentium.Praxis.Features.Storage;

/// <summary>
/// The object storage that holds workspace file content.
/// </summary>
internal interface ILocalFileService
{
    /// <returns>The new blob's id, to store as <see cref="Entities.ProjectFile.BlobName"/>.</returns>
    Task<Guid> UploadToWorkspaceAsync(Stream fileStream, string fileName, Guid? workspaceId, CancellationToken ct = default);

    /// <exception cref="FileNotFoundException">The blob does not exist.</exception>
    Task<Stream> GetFileStreamAsync(Guid blobName, CancellationToken ct = default);

    /// <summary>
    /// Idempotent: safe to call when the blob is absent.
    /// </summary>
    Task DeleteFileAsync(Guid blobName, CancellationToken ct = default);
}
