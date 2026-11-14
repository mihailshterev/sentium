using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Sentium.Praxis.Contracts;
using Sentium.Praxis.Data;

namespace Sentium.Praxis.Features.Tools.Workspace;

/// <summary>
/// An agent tool that lists the acting user's workspaces with their metadata and file counts.
/// </summary>
[AgentToolPolicy(RiskLevel = ToolRiskLevel.Low, Resource = ToolResource.File, Action = ToolAction.Read)]
internal sealed class ListWorkspacesTool(PraxisDbContext dbContext, IPdpContextAccessor runContext) : IAgentTool
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public AIFunction CreateFunction()
    {
        return AgentToolFunction.Create(
            this,
            ListAsync,
            ToolNames.ListWorkspaces,
            "Lists all available workspaces with their names, descriptions, and file counts. " +
            "Call this tool first to discover workspace names before using list_workspace_files or read_file_content.");
    }

    public async Task<string> ListAsync(CancellationToken ct = default)
    {
        if (runContext.UserId is not { } userId)
        {
            return WorkspaceToolScope.NoUserError;
        }

        try
        {
            var workspaces = await dbContext.Workspaces
                .Where(w => w.UserId == userId)
                .OrderBy(w => w.Name)
                .Select(w => new
                {
                    w.Id,
                    w.Name,
                    w.Description,
                    FileCount = w.Files.Count(f => f.UserId == userId),
                    w.CreatedAt
                })
                .ToListAsync(ct);

            return workspaces.Count == 0 ? "Notice: No workspaces found." : JsonSerializer.Serialize(workspaces, JsonOptions);
        }
        catch (Exception ex)
        {
            return $"Error: Failed to list workspaces: {ex.Message}";
        }
    }
}
