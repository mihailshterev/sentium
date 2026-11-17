namespace Sentium.Praxis.Features.Workspaces;

internal sealed record UploadRulesResponse(IReadOnlyList<string> Extensions, long MaxFileSizeBytes);
