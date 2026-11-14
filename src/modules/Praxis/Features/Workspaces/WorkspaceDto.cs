namespace Sentium.Praxis.Features.Workspaces;

internal sealed record WorkspaceDto(
    Guid Id,
    string Name,
    string? Description,
    int FileCount,
    DateTime CreatedAt,
    DateTime UpdatedAt);
