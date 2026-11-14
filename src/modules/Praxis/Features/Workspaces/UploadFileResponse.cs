namespace Sentium.Praxis.Features.Workspaces;

internal sealed record UploadFileResponse(
    Guid FileId,
    string FileName);
