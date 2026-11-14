namespace Sentium.Praxis.Features.Workspaces;

public sealed record ProjectFileResponse(
    Guid Id,
    string FileName,
    long SizeBytes,
    string Extension,
    string ProcessingStatus,
    DateTime CreatedAt,
    Guid? WorkspaceId);
