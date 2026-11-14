namespace Sentium.Praxis.Features.Workspaces;

public sealed record UploadFileResponse(
    Guid FileId,
    string FileName);
