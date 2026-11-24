namespace Sentium.Praxis.Features.Workspaces;

internal interface IWorkspaceRepository
{
    /// <summary>
    /// Most recent first.
    /// </summary>
    Task<IReadOnlyList<WorkspaceDto>> GetWorkspacesAsync(CancellationToken ct = default);

    /// <summary>
    /// Most recent first.
    /// </summary>
    Task<(IReadOnlyList<WorkspaceDto> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, CancellationToken ct = default);

    Task<WorkspaceDto?> GetWorkspaceAsync(Guid id, CancellationToken ct = default);

    Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken ct = default);

    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);

    Task<WorkspaceDto> CreateWorkspaceAsync(CreateWorkspaceRequest request, CancellationToken ct = default);

    Task<WorkspaceDto?> UpdateWorkspaceAsync(Guid id, UpdateWorkspaceRequest request, CancellationToken ct = default);

    /// <summary>
    /// Deletes the workspace row only; its files must be removed first (the foreign key would just detach them).
    /// </summary>
    Task<bool> DeleteWorkspaceAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Newest first.
    /// </summary>
    Task<IReadOnlyList<WorkspaceFileDto>> GetWorkspaceFilesAsync(Guid workspaceId, CancellationToken ct = default);

    /// <summary>
    /// A page of a workspace's files, newest first, optionally only those whose name contains
    /// <paramref name="search"/> (case-insensitive), plus how many match.
    /// </summary>
    Task<(IReadOnlyList<WorkspaceFileDto> Items, int TotalCount)> GetWorkspaceFilesPageAsync(
        Guid workspaceId,
        int page,
        int pageSize,
        string? search,
        CancellationToken ct = default);

    /// <summary>
    /// The newest files (at most the list cap) of one workspace, or of all when <paramref name="workspaceId"/> is
    /// <see langword="null"/>, whose name contains <paramref name="search"/> (case-insensitive).
    /// </summary>
    Task<IReadOnlyList<WorkspaceFileDto>> GetFilesAsync(Guid? workspaceId, string? search = null, CancellationToken ct = default);

    /// <summary>
    /// Records an uploaded file as Pending; call after its content is stored.
    /// </summary>
    Task<WorkspaceFileDto> AddFileRecordAsync(AddFileRecord record, CancellationToken ct = default);

    /// <summary>
    /// The current user's workspace with exactly this name, created when missing; safe against a concurrent create.
    /// </summary>
    Task<Guid> GetOrCreateIdAsync(string name, string description, CancellationToken ct = default);

    Task<StoredFileRecord?> GetStoredFileAsync(Guid fileId, CancellationToken ct = default);

    Task<IReadOnlyList<StoredFileRecord>> GetStoredFilesAsync(Guid workspaceId, CancellationToken ct = default);

    /// <summary>
    /// Marks a file Pending again and clears its last ingestion error.
    /// </summary>
    /// <returns><see langword="false"/> when the file does not exist.</returns>
    Task<bool> ResetForReindexAsync(Guid fileId, CancellationToken ct = default);

    /// <summary>
    /// Call after the file's stored content is deleted.
    /// </summary>
    Task DeleteFileRecordAsync(Guid fileId, CancellationToken ct = default);
}
