namespace Sentium.Praxis.Features.Workspaces;

internal sealed record WorkspaceFileDto(
    Guid Id,
    string FileName,
    string Extension,
    long SizeBytes,
    Guid? WorkspaceId,
    string ProcessingStatus,
    DateTime CreatedAt);
