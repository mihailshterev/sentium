using Sentium.AgentRuntime.Core.Dtos;
using Sentium.AgentRuntime.Core.Storage;
using Sentium.AgentRuntime.Core.Workspaces;
using Sentium.Infrastructure.Caching;
using Sentium.Infrastructure.Messaging;
using Sentium.Shared.Results;

namespace Sentium.AgentRuntime.Application.WorkspaceManagement;

public sealed class WorkspaceService(
    IWorkspaceRepository repository,
    ILocalFileService fileService,
    IEventBus eventBus,
    IScopedCache cache) : IWorkspaceService
{
    private const string CacheTag = "workspaces";

    public async Task<IReadOnlyList<WorkspaceDto>> GetWorkspacesAsync(CancellationToken ct = default)
        => await cache.GetOrCreateAsync(
            $"{CacheTag}:all",
            async token => await repository.GetWorkspacesAsync(token),
            CacheTag,
            ct);

    public async Task<PagedResponse<WorkspaceDto>> GetWorkspacesPagedAsync(int page, int pageSize, CancellationToken ct = default)
    {
        (page, pageSize) = new PaginationQuery { Page = page, PageSize = pageSize }.Normalize();

        return await cache.GetOrCreateAsync(
            $"{CacheTag}:page:{page}:{pageSize}",
            async token =>
            {
                var (items, total) = await repository.GetPagedAsync(page, pageSize, token);
                return PagedResponse<WorkspaceDto>.Create(items, total, page, pageSize);
            },
            CacheTag,
            ct);
    }

    public async Task<WorkspaceDto?> GetWorkspaceAsync(Guid id, CancellationToken ct = default)
        => await cache.GetOrCreateAsync(
            $"{CacheTag}:{id}",
            async token => await repository.GetWorkspaceAsync(id, token),
            CacheTag,
            ct);

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

    public async Task<IReadOnlyList<WorkspaceFileDto>> GetFilesAsync(Guid? workspaceId, CancellationToken ct = default)
        => await cache.GetOrCreateAsync(
            $"{CacheTag}:files:{workspaceId?.ToString() ?? "all"}",
            async token => await repository.GetFilesAsync(workspaceId, token),
            CacheTag,
            ct);

    public async Task<WorkspaceFileDto?> UploadFileAsync(Stream content, string fileName, Guid? workspaceId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (workspaceId.HasValue && !await repository.ExistsAsync(workspaceId.Value, ct))
        {
            return null;
        }

        var extension = Path.GetExtension(fileName);
        var blobName = await fileService.UploadToWorkspaceAsync(content, fileName, workspaceId, ct);

        var fileDto = await repository.AddFileRecordAsync(new AddFileRecord(fileName, blobName, extension, content.Length, workspaceId), ct);

        await cache.InvalidateTagAsync(CacheTag, ct);
        await eventBus.PublishAsync(FileEvents.FileIngested, new FileIngestedEvent(fileDto.Id, fileDto.WorkspaceId), ct: ct);

        return fileDto;
    }

    public async Task<bool> DeleteFileAsync(Guid fileId, CancellationToken ct = default)
    {
        var file = await repository.GetFileForDeletionAsync(fileId, ct);
        if (file is null)
        {
            return false;
        }

        await fileService.DeleteFileAsync(file.BlobName, ct);
        await repository.DeleteFileRecordAsync(fileId, ct);
        await cache.InvalidateTagAsync(CacheTag, ct);
        return true;
    }
}
