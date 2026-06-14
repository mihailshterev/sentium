namespace Sentium.AgentRuntime.Core.Storage;

/// <summary>
/// Abstraction over the cloud blob storage that holds workspace file content.
/// </summary>
public interface ILocalFileService
{
    /// <summary>
    /// Uploads a file stream to workspace blob storage with associated metadata.
    /// </summary>
    /// <returns>A new GUID identifying the blob, to be stored as the file's <see cref="Core.Entities.ProjectFile.BlobName"/>.</returns>
    Task<Guid> UploadToWorkspaceAsync(Stream fileStream, string fileName, Guid? workspaceId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves the content stream of a stored file by its blob identifier.
    /// </summary>
    /// <exception cref="Azure.RequestFailedException">Thrown if the blob does not exist or cannot be accessed.</exception>
    Task<Stream> GetFileStreamAsync(Guid blobName, CancellationToken ct = default);

    /// <summary>
    /// Deletes a file from blob storage by its identifier. Idempotent: safe to call when the blob is absent.
    /// </summary>
    Task DeleteFileAsync(Guid blobName, CancellationToken ct = default);
}
