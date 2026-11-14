namespace Sentium.Praxis.Features.Workspaces;

internal sealed record AddFileRecord(
    string FileName,
    Guid BlobName,
    string Extension,
    long SizeBytes,
    Guid? WorkspaceId);
