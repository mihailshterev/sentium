using Sentium.AgentRuntime.Core.Dtos;

namespace Sentium.AgentRuntime.Core.Workspaces;

/// <summary>
/// Data access and persistence for workspaces and their associated files.
/// </summary>
public interface IWorkspaceRepository
{
    /// <summary>
    /// Retrieves all workspaces from the system.
    /// </summary>
    /// <returns>All workspace DTOs, ordered by most recent first.</returns>
    Task<IReadOnlyList<WorkspaceDto>> GetWorkspacesAsync(CancellationToken ct = default);

    /// <summary>
    /// Retrieves a page of workspaces (most recent first) plus the total count.
    /// </summary>
    Task<(IReadOnlyList<WorkspaceDto> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a specific workspace by its identifier.
    /// </summary>
    /// <returns>The workspace DTO if found; otherwise <c>null</c>.</returns>
    Task<WorkspaceDto?> GetWorkspaceAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Checks whether a workspace with the given name already exists, optionally excluding one id (for updates).
    /// </summary>
    Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken ct = default);

    /// <summary>
    /// Checks whether a workspace with the given identifier exists.
    /// </summary>
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Creates a new workspace with the provided metadata.
    /// </summary>
    /// <returns>The newly created workspace DTO with a 0 file count.</returns>
    Task<WorkspaceDto> CreateWorkspaceAsync(CreateWorkspaceRequest request, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing workspace's metadata.
    /// </summary>
    /// <returns>The updated workspace DTO, or <c>null</c> if the workspace is not found.</returns>
    Task<WorkspaceDto?> UpdateWorkspaceAsync(Guid id, UpdateWorkspaceRequest request, CancellationToken ct = default);

    /// <summary>
    /// Deletes a workspace and its associated files.
    /// </summary>
    /// <returns><c>true</c> if the workspace was deleted; <c>false</c> if it was not found.</returns>
    Task<bool> DeleteWorkspaceAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all files within a specific workspace.
    /// </summary>
    /// <returns>The files in the workspace, ordered by creation date descending.</returns>
    Task<IReadOnlyList<WorkspaceFileDto>> GetWorkspaceFilesAsync(Guid workspaceId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves files, optionally filtered by workspace (all files when <paramref name="workspaceId"/> is null).
    /// </summary>
    /// <returns>The matching files, ordered by creation date descending.</returns>
    Task<IReadOnlyList<WorkspaceFileDto>> GetFilesAsync(Guid? workspaceId, CancellationToken ct = default);

    /// <summary>
    /// Adds a file record to the system (called after blob upload).
    /// </summary>
    /// <returns>The file DTO with initial Pending processing status.</returns>
    Task<WorkspaceFileDto> AddFileRecordAsync(AddFileRecord record, CancellationToken ct = default);

    /// <summary>
    /// Gathers the blob and workspace info needed to delete a file.
    /// </summary>
    /// <returns>The delete metadata if the file exists; otherwise <c>null</c>.</returns>
    Task<DeleteFileRecord?> GetFileForDeletionAsync(Guid fileId, CancellationToken ct = default);

    /// <summary>
    /// Deletes a file record from the system (called after blob deletion).
    /// </summary>
    Task DeleteFileRecordAsync(Guid fileId, CancellationToken ct = default);
}
