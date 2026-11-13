namespace Sentium.Praxis.Features.Workspaces;

public sealed record ProjectFileResponse(
    Guid Id,
    string FileName,
    long SizeBytes,
    string Extension,
    string ProcessingStatus,
    DateTime CreatedAt,
    Guid? WorkspaceId);

public sealed record UploadFileResponse(
    Guid FileId,
    string FileName);

public sealed record WorkspaceFileDto(
    Guid Id,
    string FileName,
    string Extension,
    long SizeBytes,
    Guid? WorkspaceId,
    string ProcessingStatus,
    DateTime CreatedAt);

public sealed record WorkspaceDto(
    Guid Id,
    string Name,
    string? Description,
    int FileCount,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CreateWorkspaceRequest(
    string Name,
    string? Description);

public sealed record UpdateWorkspaceRequest(
    string Name,
    string? Description);

public sealed record AddFileRecord(
    string FileName,
    Guid BlobName,
    string Extension,
    long SizeBytes,
    Guid? WorkspaceId);

/// <summary>
/// What a coordinated deletion needs to remove both the database record and the blob.
/// </summary>
public sealed record DeleteFileRecord(
    Guid FileId,
    Guid BlobName,
    Guid? WorkspaceId);
