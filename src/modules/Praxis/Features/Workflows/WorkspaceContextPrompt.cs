namespace Sentium.Praxis.Features.Workflows;

internal static class WorkspaceContextPrompt
{
    public static string Augment(string baseText, Guid? workspaceId)
    {
        return workspaceId is not { } id
            ? baseText
            : $"{baseText}\n\n[Workspace context: ID = {id}. Use list_workspaces and list_workspace_files tools to discover and read files in this workspace before answering.]";
    }
}
