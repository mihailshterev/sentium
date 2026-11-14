namespace Sentium.Praxis.Features.Workspaces;

/// <summary>
/// What a coordinated deletion needs to remove both the database record and the blob.
/// </summary>
internal sealed record DeleteFileRecord(
    Guid FileId,
    Guid BlobName,
    Guid? WorkspaceId);
