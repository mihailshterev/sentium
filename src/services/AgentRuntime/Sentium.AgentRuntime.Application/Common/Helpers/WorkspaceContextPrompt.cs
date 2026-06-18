using System.Text.Json;

namespace Sentium.AgentRuntime.Application.Common.Helpers;

internal static class WorkspaceContextPrompt
{
    public static string? TryExtract(string payload)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (doc.RootElement.TryGetProperty("workspaceId", out var wsProp) && wsProp.ValueKind == JsonValueKind.String && Guid.TryParse(wsProp.GetString(), out _))
            {
                return wsProp.GetString();
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }

    public static string Augment(string baseText, string? workspaceId)
        => string.IsNullOrEmpty(workspaceId)
            ? baseText
            : $"{baseText}\n\n[Workspace context: ID = {workspaceId}. Use list_workspaces and list_workspace_files tools to discover and read files in this workspace before answering.]";
}
