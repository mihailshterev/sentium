using Sentium.AgentRuntime.Core.Tools;
using Sentium.AgentRuntime.Core.Tools.Attributes;
using Sentium.AgentRuntime.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Sentium.AgentRuntime.Infrastructure.Tools.Workspace;

/// <summary>
/// An agent tool that lists all files within a specific workspace.
/// </summary>
/// <remarks>
/// <para>
/// This tool is marked as <see cref="ToolRiskLevel.Low"/> risk as it only performs read-only operations.
/// </para>
/// <para>
/// The tool accepts either a workspace GUID or workspace name (case-insensitive):
/// - Input: <c>{"input": "550e8400-e29b-41d4-a716-446655440000"}</c> (GUID)
/// - Input: <c>{"input": "my-workspace"}</c> (name)
/// </para>
/// <para>
/// Output format: A JSON array of file objects within the workspace, each containing:
/// - Id (GUID)
/// - FileName
/// - Extension
/// - SizeBytes
/// - Status (Pending, Processing, Completed, or Failed)
/// - CreatedAt (timestamp)
/// </para>
/// <para>
/// Files are sorted by creation date (most recent first).
/// </para>
/// </remarks>
[AgentToolPolicy(RiskLevel = ToolRiskLevel.Low)]
public sealed class ListWorkspaceFilesTool(AgentRuntimeDbContext dbContext) : IAgentTool
{
    private static readonly JsonSerializerOptions jsonOptions = new()
    {
        WriteIndented = true
    };

    /// <inheritdoc/>
    public string Name => "list_workspace_files";

    /// <inheritdoc/>
    public string Description => "Lists all files available in a workspace. " +
                                 "Accepts a workspace ID (GUID) or workspace name as input. " +
                                 "Call this tool with either: {\"input\": \"<workspace-guid>\"} or {\"input\": \"<workspace-name>\"}";

    /// <inheritdoc/>
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
                // Try resolving by name (case-insensitive)
                var workspace = await dbContext.Workspaces
                    .Where(w => w.Name.ToLower() == trimmed.ToLower())
                    .Select(w => new { w.Id, w.Name })
                    .FirstOrDefaultAsync(ct);

                if (workspace is null)
                    return $"Error: No workspace found with name or ID '{trimmed}'.";

                workspaceId = workspace.Id;
            }

            var files = await dbContext.ProjectFiles
                .Where(f => f.WorkspaceId == workspaceId)
                .OrderByDescending(f => f.CreatedAt)
                .Select(f => new
                {
                    f.Id,
                    f.FileName,
                    f.Extension,
                    f.SizeBytes,
                    Status = f.ProcessingStatus.ToString(),
                    f.CreatedAt
                })
                .ToListAsync(ct);

            if (files.Count == 0)
                return "Notice: No files found in this workspace.";

            return JsonSerializer.Serialize(files, jsonOptions);
        }
        catch (Exception ex)
        {
            return $"Error: Failed to list workspace files: {ex.Message}";
        }
    }
}
