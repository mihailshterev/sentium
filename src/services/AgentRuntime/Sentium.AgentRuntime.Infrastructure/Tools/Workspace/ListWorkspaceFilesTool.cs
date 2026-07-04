using Sentium.AgentRuntime.Core.Tools;
using Sentium.AgentRuntime.Core.Tools.Attributes;
using Sentium.AgentRuntime.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Sentium.AgentRuntime.Infrastructure.Tools.Workspace;

/// <summary>
/// An agent tool that lists all files within a specific workspace.
/// </summary>
[AgentToolPolicy(RiskLevel = ToolRiskLevel.Low)]
public sealed class ListWorkspaceFilesTool(AgentRuntimeDbContext dbContext) : IAgentTool
{
    private static readonly JsonSerializerOptions jsonOptions = new()
    {
        WriteIndented = true
    };

    public string Name => "list_workspace_files";

    public string Description => "Lists all files available in a workspace, identified by its ID (GUID) or name.";

    public IReadOnlyList<AgentToolParameter> Parameters { get; } = [new("workspace", "The workspace ID (GUID) or workspace name to list files from.")];

    public async Task<string> ExecuteAsync(string input, CancellationToken ct)
    {
        try
        {
            var trimmed = input.Trim();
            Guid? workspaceId = null;

            if (Guid.TryParse(trimmed, out var guid))
            {
                workspaceId = guid;
            }
            else
            {
                var workspace = await dbContext.Workspaces
                    .Where(w => w.Name.ToLower() == trimmed.ToLower())
                    .Select(w => new { w.Id, w.Name })
                    .FirstOrDefaultAsync(ct);

                if (workspace is null)
                {
                    return $"Error: No workspace found with name or ID '{trimmed}'.";
                }

                workspaceId = workspace.Id;
            }

            var files = await dbContext.ProjectFiles
                .Where(f => f.WorkspaceId == workspaceId)
                .OrderByDescending(f => f.CreatedAt)
                .Select(f => new WorkspaceFileSummary(
                    f.Id,
                    f.FileName,
                    f.Extension,
                    f.SizeBytes,
                    f.ProcessingStatus.ToString(),
                    f.CreatedAt))
                .ToListAsync(ct);

            if (files.Count == 0)
            {
                return "Notice: No files found in this workspace.";
            }

            return JsonSerializer.Serialize(files, jsonOptions);
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
