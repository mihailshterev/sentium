using Sentium.AgentRuntime.Core.Dtos;
using Sentium.AgentRuntime.Core.Storage;
using Sentium.AgentRuntime.Core.Workspaces;
using Sentium.Infrastructure.Messaging;

namespace Sentium.AgentRuntime.Application.WorkspaceManagement;

public sealed class WorkspaceService(
    IWorkspaceManager manager,
    ILocalFileService fileService,
    IEventBus eventBus) : IWorkspaceService
{
    public Task<IReadOnlyList<WorkspaceDto>> GetWorkspacesAsync(CancellationToken ct = default)
        => manager.GetWorkspacesAsync(ct);

    public Task<WorkspaceDto?> GetWorkspaceAsync(Guid id, CancellationToken ct = default)
        => manager.GetWorkspaceAsync(id, ct);

    public async Task<WorkspaceDto?> CreateWorkspaceAsync(CreateWorkspaceRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (await manager.NameExistsAsync(request.Name, ct: ct))
        {
            return null;
        }

        return await manager.CreateWorkspaceAsync(request, ct);
    }

    public async Task<WorkspaceDto?> UpdateWorkspaceAsync(Guid id, UpdateWorkspaceRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!await manager.ExistsAsync(id, ct))
        {
            return null;
        }

        if (await manager.NameExistsAsync(request.Name, excludeId: id, ct: ct))
        {
            throw new InvalidOperationException($"A workspace named '{request.Name}' already exists.");
        }

        return await manager.UpdateWorkspaceAsync(id, request, ct);
    }

    public async Task<bool> DeleteWorkspaceAsync(Guid id, CancellationToken ct = default)
    {
        if (!await manager.ExistsAsync(id, ct))
        {
            return false;
        }

        await manager.DeleteWorkspaceAsync(id, ct);
        return true;
    }

    public async Task<IReadOnlyList<WorkspaceFileDto>> GetWorkspaceFilesAsync(Guid workspaceId, CancellationToken ct = default)
    {
        if (!await manager.ExistsAsync(workspaceId, ct))
        {
            throw new KeyNotFoundException($"Workspace {workspaceId} not found.");
        }

        return await manager.GetWorkspaceFilesAsync(workspaceId, ct);
    }

    public Task<IReadOnlyList<WorkspaceFileDto>> GetFilesAsync(Guid? workspaceId, CancellationToken ct = default)
        => manager.GetFilesAsync(workspaceId, ct);

    public async Task<WorkspaceFileDto?> UploadFileAsync(Stream content, string fileName, Guid? workspaceId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (workspaceId.HasValue && !await manager.ExistsAsync(workspaceId.Value, ct))
        {
            return null;
        }

        var extension = Path.GetExtension(fileName);
        var blobName = await fileService.UploadToWorkspaceAsync(content, fileName, workspaceId, ct);

        var fileDto = await manager.AddFileRecordAsync(new AddFileRecord(fileName, blobName, extension, content.Length, workspaceId), ct);

        await eventBus.PublishAsync(FileEvents.FileIngested, new FileIngestedEvent(fileDto.Id, fileDto.WorkspaceId), ct: ct);

        return fileDto;
    }

    public async Task<bool> DeleteFileAsync(Guid fileId, CancellationToken ct = default)
    {
        var file = await manager.GetFileForDeletionAsync(fileId, ct);
        if (file is null)
        {
            return false;
        }

        await fileService.DeleteFileAsync(file.BlobName, ct);
        await manager.DeleteFileRecordAsync(fileId, ct);
        return true;
    }
}
