using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Sentium.Praxis.Contracts;
using Sentium.Praxis.Data;
using Sentium.Praxis.Features.Storage;

namespace Sentium.Praxis.Features.Tools.Workspace;

/// <summary>
/// An agent tool that reads and returns the full text content of one of the acting user's workspace files.
/// </summary>
[AgentToolPolicy(RiskLevel = ToolRiskLevel.Medium, Resource = ToolResource.File, Action = ToolAction.Read)]
internal sealed class ReadWorkspaceFileContentTool(PraxisDbContext dbContext, ILocalFileService fileService, IPdpContextAccessor runContext) : IAgentTool
{
    public AIFunction CreateFunction()
    {
        return AgentToolFunction.Create(
            this,
            ReadAsync,
            ToolNames.ReadWorkspaceFileContent,
            "Reads the text content of a specific file from the workspace, identified by its GUID.");
    }

    public async Task<string> ReadAsync(
        [Description("The GUID of the file to read (from list_workspace_files).")] string fileId,
        CancellationToken ct = default)
    {
        if (runContext.UserId is not { } userId)
        {
            return WorkspaceToolScope.NoUserError;
        }

        if (!Guid.TryParse(fileId?.Trim().Trim('"', '{', '}'), out var id))
        {
            return "Error: A valid file GUID is required.";
        }

        try
        {
            var projectFile = await dbContext.ProjectFiles
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId, ct);

            if (projectFile is null)
            {
                return $"Error: File with ID {id} not found.";
            }

            using var stream = await fileService.GetFileStreamAsync(projectFile.BlobName, ct);
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync(ct);
        }
        catch (Exception ex)
        {
            return $"Error: Failed to read file content: {ex.Message}";
        }
    }
}
