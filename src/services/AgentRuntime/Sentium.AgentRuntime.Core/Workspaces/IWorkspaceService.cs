using Sentium.AgentRuntime.Core.Dtos;
using Sentium.Shared.Results;

namespace Sentium.AgentRuntime.Core.Workspaces;

/// <summary>
/// Orchestration layer for workspace management, adding business rules and validation on top of the data access layer.
/// </summary>
public interface IWorkspaceService
{
    /// <summary>
    /// Retrieves all workspaces from the system.
    /// </summary>
    Task<IReadOnlyList<WorkspaceDto>> GetWorkspacesAsync(CancellationToken ct = default);

    /// <summary>
    /// Retrieves a page of workspaces (most recent first).
    /// </summary>
    Task<PagedResponse<WorkspaceDto>> GetWorkspacesPagedAsync(int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a specific workspace by its identifier.
    /// </summary>
    /// <returns>The workspace DTO if found; otherwise <c>null</c>.</returns>
    Task<WorkspaceDto?> GetWorkspaceAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Creates a new workspace with name uniqueness validation.
    /// </summary>
    /// <returns>
    /// The created workspace, or a <see cref="ResultStatus.Conflict"/> result if a workspace with that name already exists.
    /// </returns>
    Task<Result<WorkspaceDto>> CreateWorkspaceAsync(CreateWorkspaceRequest request, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing workspace with name uniqueness validation (excluding itself).
    /// </summary>
    /// <returns>
    /// The updated workspace; a <see cref="ResultStatus.NotFound"/> result if the workspace does not exist;
    /// or a <see cref="ResultStatus.Conflict"/> result when another workspace holds the same name.
    /// </returns>
    Task<Result<WorkspaceDto>> UpdateWorkspaceAsync(Guid id, UpdateWorkspaceRequest request, CancellationToken ct = default);

    /// <summary>
    /// Deletes a workspace and all its associated files.
    /// </summary>
    /// <returns><c>true</c> if the workspace was deleted; <c>false</c> if not found.</returns>
    Task<bool> DeleteWorkspaceAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all files within a specific workspace.
    /// </summary>
    /// <returns>The files in the workspace, or <c>null</c> if the workspace is not found.</returns>
    Task<IReadOnlyList<WorkspaceFileDto>?> GetWorkspaceFilesAsync(Guid workspaceId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves files, optionally filtered by workspace (all files when <paramref name="workspaceId"/> is null).
    /// </summary>
    Task<IReadOnlyList<WorkspaceFileDto>> GetFilesAsync(Guid? workspaceId, CancellationToken ct = default);

    /// <summary>
    /// Uploads a file to cloud storage and records its metadata in the database.
    /// </summary>
    /// <returns>
    /// The uploaded file DTO (created in Pending status and processed asynchronously for RAG ingestion),
    /// or <c>null</c> if the specified workspace was not found.
    /// </returns>
    Task<WorkspaceFileDto?> UploadFileAsync(Stream content, string fileName, Guid? workspaceId, CancellationToken ct = default);

    /// <summary>
    /// Deletes a file from cloud storage and removes its metadata from the database.
    /// </summary>
    /// <returns><c>true</c> if the file was deleted; <c>false</c> if the file was not found.</returns>
    Task<bool> DeleteFileAsync(Guid fileId, CancellationToken ct = default);
}
