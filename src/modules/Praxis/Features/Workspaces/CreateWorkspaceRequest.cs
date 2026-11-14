namespace Sentium.Praxis.Features.Workspaces;

internal sealed record CreateWorkspaceRequest(
    string Name,
    string? Description);
