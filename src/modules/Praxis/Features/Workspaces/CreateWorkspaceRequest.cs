namespace Sentium.Praxis.Features.Workspaces;

public sealed record CreateWorkspaceRequest(
    string Name,
    string? Description);
