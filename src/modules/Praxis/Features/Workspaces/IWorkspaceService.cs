using Sentium.Core.Results;

namespace Sentium.Praxis.Features.Workspaces;

internal interface IWorkspaceService
{
    Task<IReadOnlyList<WorkspaceDto>> GetWorkspacesAsync(CancellationToken ct = default);

    /// <summary>
    /// Most recent first.
    /// </summary>
    Task<PagedResponse<WorkspaceDto>> GetWorkspacesPagedAsync(int page, int pageSize, CancellationToken ct = default);

    Task<WorkspaceDto?> GetWorkspaceAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// <see cref="ResultStatus.Conflict"/> when the name is taken.
    /// </summary>
    Task<Result<WorkspaceDto>> CreateWorkspaceAsync(CreateWorkspaceRequest request, CancellationToken ct = default);

    /// <summary>
    /// <see cref="ResultStatus.Conflict"/> when another workspace holds the name.
    /// </summary>
    Task<Result<WorkspaceDto>> UpdateWorkspaceAsync(Guid id, UpdateWorkspaceRequest request, CancellationToken ct = default);

    /// <summary>
    /// Deletes a workspace together with its files: their index entries, stored content and records.
    /// </summary>
    Task<bool> DeleteWorkspaceAsync(Guid id, CancellationToken ct = default);

    /// <returns><see langword="null"/> when the workspace does not exist.</returns>
    Task<IReadOnlyList<WorkspaceFileDto>?> GetWorkspaceFilesAsync(Guid workspaceId, CancellationToken ct = default);

    /// <summary>
    /// A page of a workspace's files, newest first, optionally filtered by a part of their name.
    /// </summary>
    /// <returns><see langword="null"/> when the workspace does not exist.</returns>
    Task<PagedResponse<WorkspaceFileDto>?> GetWorkspaceFilesPageAsync(Guid workspaceId, int page, int pageSize, string? search, CancellationToken ct = default);

    /// <summary>
    /// Retrieves files, optionally filtered by workspace (all files when <paramref name="workspaceId"/> is null) and by
    /// a part of their name.
    /// </summary>
    Task<IReadOnlyList<WorkspaceFileDto>> GetFilesAsync(Guid? workspaceId, string? search = null, CancellationToken ct = default);

    /// <summary>
    /// Stores the file as Pending for background ingestion. Without a workspace it goes to the user's "Chat uploads"
    /// workspace, created on first use.
    /// </summary>
    /// <returns><see langword="null"/> when <paramref name="workspaceId"/> does not exist.</returns>
    Task<WorkspaceFileDto?> UploadFileAsync(Stream content, string fileName, Guid? workspaceId, CancellationToken ct = default);

    Task<bool> DeleteFileAsync(Guid fileId, CancellationToken ct = default);

    /// <summary>
    /// Queues a file for ingestion again (e.g. after a failure), clearing its last error.
    /// </summary>
    /// <returns><see langword="false"/> when the file does not exist.</returns>
    Task<bool> ReindexFileAsync(Guid fileId, CancellationToken ct = default);

    /// <summary>
    /// Opens a file's original content; <see langword="null"/> when the file or its stored content is missing.
    /// </summary>
    Task<WorkspaceFileContent?> OpenFileAsync(Guid fileId, CancellationToken ct = default);

    /// <summary>
    /// The file's extracted text, cut at <paramref name="maxChars"/>. <see cref="ResultStatus.Invalid"/> when the
    /// document cannot be parsed.
    /// </summary>
    Task<Result<WorkspaceFileText>> GetFileTextAsync(Guid fileId, int maxChars, CancellationToken ct = default);
}
