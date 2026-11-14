namespace Sentium.Praxis.Features.Workspaces;

public sealed record UpdateWorkspaceRequest(
    string Name,
    string? Description);
