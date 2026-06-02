using Sentium.AgentRuntime.Core.Dtos;
using Sentium.AgentRuntime.Core.Entities;
using Sentium.AgentRuntime.Core.Files;
using Sentium.AgentRuntime.Core.Workspaces;
using Sentium.AgentRuntime.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Sentium.AgentRuntime.Infrastructure.WorkspaceManagement;

public sealed class WorkspaceRepository(AgentRuntimeDbContext context) : IWorkspaceRepository
{
    public async Task<IReadOnlyList<WorkspaceDto>> GetWorkspacesAsync(CancellationToken ct = default)
    {
        return await context.Workspaces
            .AsNoTracking()
            .OrderByDescending(w => w.CreatedAt)
            .Select(w => new WorkspaceDto(
                w.Id,
                w.Name,
                w.Description,
                w.Files.Count,
                w.CreatedAt,
                w.UpdatedAt))
            .ToListAsync(ct);
    }

    public async Task<WorkspaceDto?> GetWorkspaceAsync(Guid id, CancellationToken ct = default)
    {
        return await context.Workspaces
            .AsNoTracking()
            .Where(w => w.Id == id)
            .Select(w => new WorkspaceDto(
                w.Id,
                w.Name,
                w.Description,
                w.Files.Count,
                w.CreatedAt,
                w.UpdatedAt))
            .FirstOrDefaultAsync(ct);
    }

    public Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken ct = default)
    {
        return context.Workspaces
            .AnyAsync(w => w.Name == name && (excludeId == null || w.Id != excludeId.Value), ct);
    }

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
    {
        return context.Workspaces.AnyAsync(w => w.Id == id, ct);
    }

    public async Task<WorkspaceDto> CreateWorkspaceAsync(CreateWorkspaceRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var now = DateTime.UtcNow;
        var workspace = new Workspace
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
        };

        context.Workspaces.Add(workspace);
        await context.SaveChangesAsync(ct);

        return new WorkspaceDto(workspace.Id, workspace.Name, workspace.Description, 0, workspace.CreatedAt, workspace.UpdatedAt);
    }

    public async Task<WorkspaceDto?> UpdateWorkspaceAsync(Guid id, UpdateWorkspaceRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var workspace = await context.Workspaces.FindAsync([id], ct);
        if (workspace is null)
        {
            return null;
        }

        workspace.Name = request.Name.Trim();
        workspace.Description = request.Description?.Trim();
        workspace.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(ct);

        var fileCount = await context.ProjectFiles.CountAsync(f => f.WorkspaceId == id, ct);
        return new WorkspaceDto(workspace.Id, workspace.Name, workspace.Description, fileCount, workspace.CreatedAt, workspace.UpdatedAt);
    }

    public async Task<bool> DeleteWorkspaceAsync(Guid id, CancellationToken ct = default)
    {
        var affectedRows = await context.Workspaces
            .Where(w => w.Id == id)
            .ExecuteDeleteAsync(ct);

        return affectedRows > 0;
    }

    public async Task<IReadOnlyList<WorkspaceFileDto>> GetWorkspaceFilesAsync(Guid workspaceId, CancellationToken ct = default)
    {
        return await context.ProjectFiles
            .AsNoTracking()
            .Where(f => f.WorkspaceId == workspaceId)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new WorkspaceFileDto(
                f.Id, f.FileName, f.Extension, f.SizeBytes,
                f.WorkspaceId, f.ProcessingStatus.ToString(), f.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<WorkspaceFileDto>> GetFilesAsync(Guid? workspaceId, CancellationToken ct = default)
    {
        var query = context.ProjectFiles.AsNoTracking().AsQueryable();

        if (workspaceId.HasValue)
        {
            query = query.Where(f => f.WorkspaceId == workspaceId.Value);
        }

        return await query
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new WorkspaceFileDto(
                f.Id, f.FileName, f.Extension, f.SizeBytes,
                f.WorkspaceId, f.ProcessingStatus.ToString(), f.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<WorkspaceFileDto> AddFileRecordAsync(AddFileRecord record, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        var projectFile = new ProjectFile
        {
            Id = Guid.NewGuid(),
            FileName = record.FileName,
            BlobName = record.BlobName,
            Extension = record.Extension,
            SizeBytes = record.SizeBytes,
            WorkspaceId = record.WorkspaceId,
            ProcessingStatus = FileProcessingStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        context.ProjectFiles.Add(projectFile);
        await context.SaveChangesAsync(ct);

        return new WorkspaceFileDto(
            projectFile.Id, projectFile.FileName, projectFile.Extension, projectFile.SizeBytes,
            projectFile.WorkspaceId, projectFile.ProcessingStatus.ToString(), projectFile.CreatedAt);
    }

    public async Task<DeleteFileRecord?> GetFileForDeletionAsync(Guid fileId, CancellationToken ct = default)
    {
        return await context.ProjectFiles
            .AsNoTracking()
            .Where(f => f.Id == fileId)
            .Select(f => new DeleteFileRecord(f.Id, f.BlobName, f.WorkspaceId))
            .FirstOrDefaultAsync(ct);
    }

    public async Task DeleteFileRecordAsync(Guid fileId, CancellationToken ct = default)
    {
        await context.ProjectFiles
            .Where(f => f.Id == fileId)
            .ExecuteDeleteAsync(ct);
    }
}

