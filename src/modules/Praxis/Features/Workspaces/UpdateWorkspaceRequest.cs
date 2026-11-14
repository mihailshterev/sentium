namespace Sentium.Praxis.Features.Workspaces;

internal sealed record UpdateWorkspaceRequest(
    string Name,
    string? Description);
