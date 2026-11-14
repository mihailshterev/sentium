using System.ComponentModel;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Sentium.Praxis.Contracts;
using Sentium.Praxis.Data;

namespace Sentium.Praxis.Features.Tools.Workspace;

/// <summary>
/// An agent tool that lists the files within one of the acting user's workspaces.
/// </summary>
[AgentToolPolicy(RiskLevel = ToolRiskLevel.Low, Resource = ToolResource.File, Action = ToolAction.Read)]
public sealed class ListWorkspaceFilesTool(PraxisDbContext dbContext, IPdpContextAccessor runContext) : IAgentTool
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public AIFunction CreateFunction()
    {
        return AgentToolFunction.Create(
            this,
            ListAsync,
            ToolNames.ListWorkspaceFiles,
            "Lists all files available in a workspace, identified by its ID (GUID) or name.");
    }

    public async Task<string> ListAsync(
        [Description("The workspace ID (GUID) or workspace name to list files from.")] string workspace,
        CancellationToken ct = default)
    {
        if (runContext.UserId is not { } userId)
        {
            return WorkspaceToolScope.NoUserError;
        }

        if (string.IsNullOrWhiteSpace(workspace))
        {
            return "Error: A workspace ID or name is required.";
        }

        try
        {
            var workspaceId = await WorkspaceToolScope.ResolveWorkspaceIdAsync(dbContext, userId, workspace, ct);
            if (workspaceId is null)
            {
                return $"Error: No workspace found with name or ID '{workspace.Trim()}'.";
            }

            var files = await dbContext.ProjectFiles
                .Where(f => f.WorkspaceId == workspaceId && f.UserId == userId)
                .OrderByDescending(f => f.CreatedAt)
                .Select(f => new WorkspaceFileSummary(
                    f.Id,
                    f.FileName,
                    f.Extension,
                    f.SizeBytes,
                    f.ProcessingStatus.ToString(),
                    f.CreatedAt))
                .ToListAsync(ct);

            return files.Count == 0 ? "Notice: No files found in this workspace." : JsonSerializer.Serialize(files, JsonOptions);
        }
        catch (Exception ex)
        {
            return $"Error: Failed to list workspace files: {ex.Message}";
        }
    }

    private sealed record WorkspaceFileSummary(
        Guid Id,
        string FileName,
        string Extension,
        long SizeBytes,
        string Status,
        DateTime CreatedAt);
}
