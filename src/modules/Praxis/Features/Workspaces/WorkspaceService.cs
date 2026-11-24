using Sentium.Codex.Contracts;
using Sentium.Core.Results;
using Sentium.Infrastructure.Caching;
using Sentium.Praxis.Common;
using Sentium.Praxis.Features.Storage;

namespace Sentium.Praxis.Features.Workspaces;

internal sealed class WorkspaceService(
    IWorkspaceRepository repository,
    ILocalFileService fileService,
    IWorkspaceFileTextReader textReader,
    IFileIngestionQueue ingestionQueue,
    IDocumentIngestionService ingestionService,
    IScopedCache cache) : IWorkspaceService
{
    internal const string CacheTag = "workspaces";

    /// <summary>
    /// Where files uploaded without a workspace (e.g. dropped into a chat that has none) are kept.
    /// </summary>
    internal const string DefaultWorkspaceName = "Chat uploads";

    private const string DefaultWorkspaceDescription = "Files uploaded from chat without a workspace.";

    public async Task<IReadOnlyList<WorkspaceDto>> GetWorkspacesAsync(CancellationToken ct = default)
    {
        return await cache.GetOrCreateAsync(
            $"{CacheTag}:all",
            async token => await repository.GetWorkspacesAsync(token),
            CacheTag,
            ct);
    }

    public async Task<PagedResponse<WorkspaceDto>> GetWorkspacesPagedAsync(int page, int pageSize, CancellationToken ct = default)
    {
        (page, pageSize) = new PaginationQuery { Page = page, PageSize = pageSize }.Normalize();

        return await cache.GetOrCreateAsync(
            $"{CacheTag}:page:{page}:{pageSize}",
            token => LoadPageAsync(page, pageSize, token),
            CacheTag,
            ct);
    }

    public async Task<WorkspaceDto?> GetWorkspaceAsync(Guid id, CancellationToken ct = default)
    {
        return await cache.GetOrCreateAsync(
            $"{CacheTag}:{id}",
            async token => await repository.GetWorkspaceAsync(id, token),
            CacheTag,
            ct);
    }

    public async Task<Result<WorkspaceDto>> CreateWorkspaceAsync(CreateWorkspaceRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (await repository.NameExistsAsync(request.Name, ct: ct))
        {
            return Result<WorkspaceDto>.Conflict($"A workspace named '{request.Name}' already exists.");
        }

        var created = await repository.CreateWorkspaceAsync(request, ct);
        await cache.InvalidateTagAsync(CacheTag, ct);
        return Result<WorkspaceDto>.Success(created);
    }

    public async Task<Result<WorkspaceDto>> UpdateWorkspaceAsync(Guid id, UpdateWorkspaceRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!await repository.ExistsAsync(id, ct))
        {
            return Result<WorkspaceDto>.NotFound();
        }

        if (await repository.NameExistsAsync(request.Name, excludeId: id, ct: ct))
        {
            return Result<WorkspaceDto>.Conflict($"A workspace named '{request.Name}' already exists.");
        }

        var updated = await repository.UpdateWorkspaceAsync(id, request, ct);
        if (updated is null)
        {
            return Result<WorkspaceDto>.NotFound();
        }

        await cache.InvalidateTagAsync(CacheTag, ct);
        return Result<WorkspaceDto>.Success(updated);
    }

    public async Task<bool> DeleteWorkspaceAsync(Guid id, CancellationToken ct = default)
    {
        if (!await repository.ExistsAsync(id, ct))
        {
            return false;
        }

        foreach (var file in await repository.GetStoredFilesAsync(id, ct))
        {
            await RemoveFileAsync(file, ct);
        }

        await repository.DeleteWorkspaceAsync(id, ct);
        await cache.InvalidateTagAsync(CacheTag, ct);
        return true;
    }

    public async Task<IReadOnlyList<WorkspaceFileDto>?> GetWorkspaceFilesAsync(Guid workspaceId, CancellationToken ct = default)
    {
        if (!await repository.ExistsAsync(workspaceId, ct))
        {
            return null;
        }

        return await cache.GetOrCreateAsync(
            $"{CacheTag}:files:{workspaceId}",
            async token => await repository.GetWorkspaceFilesAsync(workspaceId, token),
            CacheTag,
            ct);
    }

    public async Task<PagedResponse<WorkspaceFileDto>?> GetWorkspaceFilesPageAsync(Guid workspaceId, int page, int pageSize, string? search, CancellationToken ct = default)
    {
        if (!await repository.ExistsAsync(workspaceId, ct))
        {
            return null;
        }

        (page, pageSize) = new PaginationQuery { Page = page, PageSize = pageSize }.Normalize();
        var term = TextSearch.Normalize(search);

        return await cache.GetOrCreateAsync(
            $"{CacheTag}:files:{workspaceId}:page:{page}:{pageSize}:q:{term?.ToLowerInvariant()}",
            token => LoadFilesPageAsync(workspaceId, page, pageSize, term, token),
            CacheTag,
            ct);
    }

    public async Task<IReadOnlyList<WorkspaceFileDto>> GetFilesAsync(Guid? workspaceId, string? search = null, CancellationToken ct = default)
    {
        var term = TextSearch.Normalize(search);
        return await cache.GetOrCreateAsync(
            $"{CacheTag}:files:{workspaceId?.ToString() ?? "all"}:q:{term?.ToLowerInvariant()}",
            token => LoadFilesAsync(workspaceId, term, token),
            CacheTag,
            ct);
    }

    public async Task<WorkspaceFileDto?> UploadFileAsync(Stream content, string fileName, Guid? workspaceId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (workspaceId.HasValue && !await repository.ExistsAsync(workspaceId.Value, ct))
        {
            return null;
        }

        var targetWorkspaceId = workspaceId ?? await GetOrCreateDefaultWorkspaceIdAsync(ct);

        var extension = Path.GetExtension(fileName);
        var blobName = await fileService.UploadToWorkspaceAsync(content, fileName, targetWorkspaceId, ct);

        var fileDto = await repository.AddFileRecordAsync(new AddFileRecord(fileName, blobName, extension, content.Length, targetWorkspaceId), ct);

        await cache.InvalidateTagAsync(CacheTag, ct);
        ingestionQueue.Enqueue(new FileIngestionRequest(fileDto.Id, fileDto.WorkspaceId));

        return fileDto;
    }

    public async Task<bool> DeleteFileAsync(Guid fileId, CancellationToken ct = default)
    {
        var file = await repository.GetStoredFileAsync(fileId, ct);
        if (file is null)
        {
            return false;
        }

        await RemoveFileAsync(file, ct);
        await cache.InvalidateTagAsync(CacheTag, ct);
        return true;
    }

    public async Task<bool> ReindexFileAsync(Guid fileId, CancellationToken ct = default)
    {
        var file = await repository.GetStoredFileAsync(fileId, ct);
        if (file is null || !await repository.ResetForReindexAsync(fileId, ct))
        {
            return false;
        }

        await cache.InvalidateTagAsync(CacheTag, ct);
        ingestionQueue.Enqueue(new FileIngestionRequest(file.FileId, file.WorkspaceId));
        return true;
    }

    public async Task<WorkspaceFileContent?> OpenFileAsync(Guid fileId, CancellationToken ct = default)
    {
        var file = await repository.GetStoredFileAsync(fileId, ct);
        if (file is null)
        {
            return null;
        }

        try
        {
            var stream = await fileService.GetFileStreamAsync(file.BlobName, ct);
            return new WorkspaceFileContent(file.FileName, file.Extension, stream);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    public async Task<Result<WorkspaceFileText>> GetFileTextAsync(Guid fileId, int maxChars, CancellationToken ct = default)
    {
        var file = await repository.GetStoredFileAsync(fileId, ct);
        if (file is null)
        {
            return Result<WorkspaceFileText>.NotFound();
        }

        string text;
        try
        {
            text = await textReader.ReadAsync(file.BlobName, file.Extension, ct);
        }
        catch (FileNotFoundException)
        {
            return Result<WorkspaceFileText>.NotFound("The file's content is missing from storage.");
        }
        catch (InvalidDataException ex)
        {
            return Result<WorkspaceFileText>.Invalid(ex.Message);
        }

        var limit = Math.Max(maxChars, 1);
        var truncated = text.Length > limit;
        return Result<WorkspaceFileText>.Success(new WorkspaceFileText(file.FileId, file.FileName, truncated ? text[..limit] : text, truncated, text.Length));
    }

    /// <summary>
    /// Index entries go first: a file whose chunks outlived its row would keep answering searches with no way to
    /// delete it.
    /// </summary>
    private async Task RemoveFileAsync(StoredFileRecord file, CancellationToken ct)
    {
        await ingestionService.RemoveDocumentAsync(WorkspaceFileMetadata.FileId, file.FileId.ToString(), ct: ct);
        await fileService.DeleteFileAsync(file.BlobName, ct);
        await repository.DeleteFileRecordAsync(file.FileId, ct);
    }

    private async Task<Guid> GetOrCreateDefaultWorkspaceIdAsync(CancellationToken ct)
    {
        var id = await repository.GetOrCreateIdAsync(DefaultWorkspaceName, DefaultWorkspaceDescription, ct);
        await cache.InvalidateTagAsync(CacheTag, ct);
        return id;
    }

    private async ValueTask<PagedResponse<WorkspaceDto>> LoadPageAsync(int page, int pageSize, CancellationToken ct)
    {
        var (items, total) = await repository.GetPagedAsync(page, pageSize, ct);
        return PagedResponse<WorkspaceDto>.Create(items, total, page, pageSize);
    }

    private async ValueTask<PagedResponse<WorkspaceFileDto>?> LoadFilesPageAsync(Guid workspaceId, int page, int pageSize, string? term, CancellationToken ct)
    {
        var (items, total) = await repository.GetWorkspaceFilesPageAsync(workspaceId, page, pageSize, term, ct);
        return PagedResponse<WorkspaceFileDto>.Create(items, total, page, pageSize);
    }

    private async ValueTask<IReadOnlyList<WorkspaceFileDto>> LoadFilesAsync(Guid? workspaceId, string? term, CancellationToken ct)
    {
        return await repository.GetFilesAsync(workspaceId, term, ct);
    }
}
