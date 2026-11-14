namespace Sentium.Praxis.Features.Workspaces;

public sealed record WorkspaceDto(
    Guid Id,
    string Name,
    string? Description,
    int FileCount,
    DateTime CreatedAt,
    DateTime UpdatedAt);
