namespace Sentium.AgentRuntime.Core.Dtos;

/// <summary>
/// Represents response data for a project file retrieved from the workspace management system.
/// </summary>
public sealed record ProjectFileResponse(
    Guid Id,
    string FileName,
    long SizeBytes,
    string Extension,
    string ProcessingStatus,
    DateTime CreatedAt,
    Guid? WorkspaceId);

/// <summary>
/// Represents response data returned after successfully uploading a file.
/// </summary>
public sealed record UploadFileResponse(
    Guid FileId,
    string FileName);

/// <summary>
/// Represents a file within a workspace, including metadata and processing status.
/// </summary>
public sealed record WorkspaceFileDto(
    Guid Id,
    string FileName,
    string Extension,
    long SizeBytes,
    Guid? WorkspaceId,
    string ProcessingStatus,
    DateTime CreatedAt);

/// <summary>
/// Represents a workspace with aggregate file count information.
/// </summary>
public sealed record WorkspaceDto(
    Guid Id,
    string Name,
    string? Description,
    int FileCount,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>
/// Represents a request to create a new workspace.
/// </summary>
public sealed record CreateWorkspaceRequest(
    string Name,
    string? Description);

/// <summary>
/// Represents a request to update an existing workspace.
/// </summary>
public sealed record UpdateWorkspaceRequest(
    string Name,
    string? Description);

/// <summary>
/// Represents metadata for a file to be added to the system.
/// </summary>
/// <remarks>
/// This record is used internally when recording file uploads in the database.
/// </remarks>
public sealed record AddFileRecord(
    string FileName,
    Guid BlobName,
    string Extension,
    long SizeBytes,
    Guid? WorkspaceId);

/// <summary>
/// Represents metadata for a file that is being deleted from the system.
/// </summary>
/// <remarks>
/// This record is used to bundle together all necessary information for coordinated deletion
/// of the database record and blob storage artifact.
/// </remarks>
public sealed record DeleteFileRecord(
    Guid FileId,
    Guid BlobName,
    Guid? WorkspaceId);
