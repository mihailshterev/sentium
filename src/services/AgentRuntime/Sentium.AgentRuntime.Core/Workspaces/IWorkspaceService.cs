using Sentium.AgentRuntime.Core.Dtos;

namespace Sentium.AgentRuntime.Core.Workspaces;

/// <summary>
/// Defines the orchestration layer for workspace management, adding business rules and validation on top of the data access layer.
/// </summary>
/// <remarks>
/// <para>
/// This interface sits between the API controllers and the data access layer, enforcing:
/// - Name uniqueness constraints.
/// - Workspace existence validation.
/// - File upload constraints and validation.
/// - Transactional consistency across workspace and file operations.
/// </para>
/// <para>
/// Unlike <see cref="IWorkspaceManager"/>, this service implements business logic
/// such as duplicate name checking and graceful error handling.
/// </para>
/// </remarks>
public interface IWorkspaceService
{
    /// <summary>
    /// Retrieves all workspaces from the system.
    /// </summary>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A read-only list of all workspace DTOs.</returns>
    Task<IReadOnlyList<WorkspaceDto>> GetWorkspacesAsync(CancellationToken ct = default);

    /// <summary>
    /// Retrieves a specific workspace by its identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the workspace.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The workspace DTO if found; otherwise <c>null</c>.</returns>
    Task<WorkspaceDto?> GetWorkspaceAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Creates a new workspace with name uniqueness validation.
    /// </summary>
    /// <param name="request">The creation request containing name and optional description.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>
    /// The created workspace, or <c>null</c> if a workspace with that name already exists.
    /// </returns>
    Task<WorkspaceDto?> CreateWorkspaceAsync(CreateWorkspaceRequest request, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing workspace with name uniqueness validation (excluding itself).
    /// </summary>
    /// <param name="id">The identifier of the workspace to update.</param>
    /// <param name="request">The update request with new name and optional description.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>
    /// The updated workspace, or <c>null</c> if the workspace is not found.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when another workspace holds the same name.
    /// </exception>
    Task<WorkspaceDto?> UpdateWorkspaceAsync(Guid id, UpdateWorkspaceRequest request, CancellationToken ct = default);

    /// <summary>
    /// Deletes a workspace and all its associated files.
    /// </summary>
    /// <param name="id">The identifier of the workspace to delete.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns><c>true</c> if the workspace was deleted; <c>false</c> if not found.</returns>
    Task<bool> DeleteWorkspaceAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all files within a specific workspace.
    /// </summary>
    /// <param name="workspaceId">The identifier of the workspace.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A read-only list of files in the workspace.</returns>
    /// <exception cref="KeyNotFoundException">Thrown if the workspace is not found.</exception>
    Task<IReadOnlyList<WorkspaceFileDto>> GetWorkspaceFilesAsync(Guid workspaceId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves files optionally filtered by workspace.
    /// </summary>
    /// <param name="workspaceId">The optional workspace identifier to filter by. If null, returns all files.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A read-only list of files.</returns>
    Task<IReadOnlyList<WorkspaceFileDto>> GetFilesAsync(Guid? workspaceId, CancellationToken ct = default);

    /// <summary>
    /// Uploads a file to cloud storage and records its metadata in the database.
    /// </summary>
    /// <param name="content">The file stream to upload.</param>
    /// <param name="fileName">The original filename provided by the user.</param>
    /// <param name="workspaceId">The optional workspace to associate the file with.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>
    /// The uploaded file DTO, or <c>null</c> if the specified workspace was not found.
    /// </returns>
    /// <remarks>
    /// The file enters the system in Pending status and will be processed asynchronously for RAG ingestion.
    /// </remarks>
    Task<WorkspaceFileDto?> UploadFileAsync(Stream content, string fileName, Guid? workspaceId, CancellationToken ct = default);

    /// <summary>
    /// Deletes a file from cloud storage and removes its metadata from the database.
    /// </summary>
    /// <param name="fileId">The identifier of the file to delete.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns><c>true</c> if the file was deleted; <c>false</c> if the file was not found.</returns>
    Task<bool> DeleteFileAsync(Guid fileId, CancellationToken ct = default);
}
