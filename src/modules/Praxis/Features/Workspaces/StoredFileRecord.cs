namespace Sentium.Praxis.Features.Workspaces;

/// <summary>
/// What reading, re-indexing or deleting a file needs beyond its DTO: where its content is stored.
/// </summary>
internal sealed record StoredFileRecord(
    Guid FileId,
    string FileName,
    Guid BlobName,
    string Extension,
    Guid? WorkspaceId);
