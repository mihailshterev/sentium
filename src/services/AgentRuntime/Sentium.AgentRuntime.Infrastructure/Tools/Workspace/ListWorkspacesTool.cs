using Sentium.AgentRuntime.Core.Tools;
using Sentium.AgentRuntime.Core.Tools.Attributes;
using Sentium.AgentRuntime.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Sentium.AgentRuntime.Infrastructure.Tools.Workspace;

/// <summary>
/// An agent tool that lists all available workspaces with their metadata and file counts.
/// </summary>
[AgentToolPolicy(RiskLevel = ToolRiskLevel.Low)]
public sealed class ListWorkspacesTool(AgentRuntimeDbContext dbContext) : IAgentTool
{
    private static readonly JsonSerializerOptions jsonOptions = new()
    {
        WriteIndented = true
    };

    public string Name => "list_workspaces";

    public string Description => "Lists all available workspaces with their names, descriptions, and file counts. " +
                                 "Call this tool first to discover workspace names before using list_workspace_files or read_file_content.";

    public async Task<string> ExecuteAsync(string input, CancellationToken ct)
    {
        try
        {
            var workspaces = await dbContext.Workspaces
                .OrderBy(w => w.Name)
                .Select(w => new
                {
                    w.Id,
                    w.Name,
                    w.Description,
                    FileCount = w.Files.Count(f => f.WorkspaceId == w.Id),
                    w.CreatedAt
                })
                .ToListAsync(ct);

            if (workspaces.Count == 0)
            {
                return "Notice: No workspaces found.";
            }

            return JsonSerializer.Serialize(workspaces, jsonOptions);
        }
        catch (Exception ex)
        {
            return $"Error: Failed to list workspaces: {ex.Message}";
        }
    }
}
