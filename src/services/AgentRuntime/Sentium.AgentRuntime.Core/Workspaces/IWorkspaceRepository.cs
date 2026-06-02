using Sentium.AgentRuntime.Core.Dtos;

namespace Sentium.AgentRuntime.Core.Workspaces;

/// <summary>
/// Defines the core business logic for managing workspaces and their associated files.
/// </summary>
/// <remarks>
/// <para>
/// This interface abstracts the data access and persistence layer for workspace operations.
/// Implementations should enforce business rules such as name uniqueness and valid state transitions.
/// </para>
/// <para>
/// The interface separates workspace management from file management for clarity:
/// - Workspace methods: CRUD operations on workspace containers.
/// - File methods: Tracking and lifecycle management of project files.
/// </para>
/// </remarks>
public interface IWorkspaceRepository
{
    /// <summary>
    /// Retrieves all workspaces from the system.
    /// </summary>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A read-only list of all workspace DTOs, ordered by most recent first.</returns>
    Task<IReadOnlyList<WorkspaceDto>> GetWorkspacesAsync(CancellationToken ct = default);

    /// <summary>
    /// Retrieves a specific workspace by its identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the workspace.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The workspace DTO if found; otherwise <c>null</c>.</returns>
    Task<WorkspaceDto?> GetWorkspaceAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Checks whether a workspace with the given name already exists.
    /// </summary>
    /// <param name="name">The workspace name to check.</param>
    /// <param name="excludeId">An optional workspace ID to exclude from the check (useful for update scenarios).</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns><c>true</c> if a workspace with this name exists; otherwise <c>false</c>.</returns>
    Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken ct = default);

    /// <summary>
    /// Checks whether a workspace with the given identifier exists.
    /// </summary>
    /// <param name="id">The workspace ID to check.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns><c>true</c> if the workspace exists; otherwise <c>false</c>.</returns>
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Creates a new workspace with the provided metadata.
    /// </summary>
    /// <param name="request">The creation request containing name and optional description.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The newly created workspace DTO with a 0 file count.</returns>
    Task<WorkspaceDto> CreateWorkspaceAsync(CreateWorkspaceRequest request, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing workspace's metadata.
    /// </summary>
    /// <param name="id">The identifier of the workspace to update.</param>
    /// <param name="request">The update request containing new name and optional description.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The updated workspace DTO, or <c>null</c> if the workspace is not found.</returns>
    Task<WorkspaceDto?> UpdateWorkspaceAsync(Guid id, UpdateWorkspaceRequest request, CancellationToken ct = default);

    /// <summary>
    /// Deletes a workspace and its associated files.
    /// </summary>
    /// <param name="id">The identifier of the workspace to delete.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns><c>true</c> if the workspace was deleted; <c>false</c> if it was not found.</returns>
    Task<bool> DeleteWorkspaceAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all files within a specific workspace.
    /// </summary>
    /// <param name="workspaceId">The identifier of the workspace.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A read-only list of files in the workspace, ordered by creation date descending.</returns>
    Task<IReadOnlyList<WorkspaceFileDto>> GetWorkspaceFilesAsync(Guid workspaceId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves files optionally filtered by workspace.
    /// </summary>
    /// <param name="workspaceId">The optional workspace identifier to filter by. If null, returns all files.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A read-only list of files, ordered by creation date descending.</returns>
    Task<IReadOnlyList<WorkspaceFileDto>> GetFilesAsync(Guid? workspaceId, CancellationToken ct = default);

    /// <summary>
    /// Adds a file record to the system (called after blob upload).
    /// </summary>
    /// <param name="record">The file metadata to add.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The file DTO with initial Pending processing status.</returns>
    Task<WorkspaceFileDto> AddFileRecordAsync(AddFileRecord record, CancellationToken ct = default);

    /// <summary>
    /// Retrieves metadata for a file that is being deleted (gathers blob and workspace info).
    /// </summary>
    /// <param name="fileId">The identifier of the file to prepare for deletion.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The delete metadata if the file exists; otherwise <c>null</c>.</returns>
    Task<DeleteFileRecord?> GetFileForDeletionAsync(Guid fileId, CancellationToken ct = default);

    /// <summary>
    /// Deletes a file record from the system (called after blob deletion).
    /// </summary>
    /// <param name="fileId">The identifier of the file to delete.</param>
    /// <param name="ct">A cancellation token.</param>
    Task DeleteFileRecordAsync(Guid fileId, CancellationToken ct = default);
}
