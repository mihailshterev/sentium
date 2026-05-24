namespace Sentium.AgentRuntime.Core.Storage;

/// <summary>
/// Defines the abstraction for managing file storage in cloud blob storage.
/// </summary>
/// <remarks>
/// <para>
/// This interface abstracts the underlying storage provider (e.g., Azure Blob Storage).
/// Files uploaded to workspaces are stored using this service, which handles blob creation,
/// retrieval, and deletion.
/// </para>
/// <para>
/// The service stores both the file content and metadata (filename, workspace association)
/// in blob storage metadata.
/// </para>
/// </remarks>
public interface ILocalFileService
{
    /// <summary>
    /// Uploads a file stream to workspace blob storage with associated metadata.
    /// </summary>
    /// <param name="fileStream">The stream containing the file content to upload.</param>
    /// <param name="fileName">The original filename (used for blob metadata).</param>
    /// <param name="workspaceId">The optional workspace ID (included in blob metadata).</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A new GUID identifying the blob in storage.</returns>
    /// <remarks>
    /// The returned GUID should be stored in the <see cref="Core.Entities.ProjectFile.BlobName"/> field
    /// for later retrieval and deletion.
    /// </remarks>
    Task<Guid> UploadToWorkspaceAsync(Stream fileStream, string fileName, Guid? workspaceId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves the content stream of a stored file by its blob identifier.
    /// </summary>
    /// <param name="blobName">The blob identifier returned from <see cref="UploadToWorkspaceAsync"/>.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A readable stream containing the file content.</returns>
    /// <exception cref="Azure.RequestFailedException">Thrown if the blob does not exist or cannot be accessed.</exception>
    Task<Stream> GetFileStreamAsync(Guid blobName, CancellationToken ct = default);

    /// <summary>
    /// Deletes a file from blob storage by its identifier.
    /// </summary>
    /// <param name="blobName">The blob identifier to delete.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <remarks>
    /// This operation is safe to call even if the blob does not exist (idempotent).
    /// </remarks>
    Task DeleteFileAsync(Guid blobName, CancellationToken ct = default);
}
