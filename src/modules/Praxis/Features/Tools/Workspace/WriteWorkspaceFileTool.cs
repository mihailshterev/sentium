using System.ComponentModel;
using System.Text;
using Microsoft.Extensions.AI;
using Sentium.Praxis.Data;
using Sentium.Praxis.Entities;
using Sentium.Praxis.Features.Files;
using Sentium.Praxis.Features.Sentinel;
using Sentium.Praxis.Features.Storage;
using Sentium.Praxis.Features.Tools.Attributes;

namespace Sentium.Praxis.Features.Tools.Workspace;

/// <summary>
/// An agent tool that saves a new text file to one of the acting user's workspaces.
/// </summary>
[AgentToolPolicy(RiskLevel = ToolRiskLevel.Medium, Resource = ToolResource.File, Action = ToolAction.Write)]
public sealed class WriteWorkspaceFileTool(
    PraxisDbContext dbContext,
    ILocalFileService fileService,
    IFileIngestionQueue ingestionQueue,
    IPdpContextAccessor runContext) : IAgentTool
{
    public AIFunction CreateFunction() => AgentToolFunction.Create(
        this,
        WriteAsync,
        ToolNames.WriteWorkspaceFile,
        "Saves a new text file to a workspace. Accepts a workspace ID (GUID) or workspace name.");

    public async Task<string> WriteAsync(
        [Description("The target workspace ID (GUID) or workspace name.")] string workspace,
        [Description("The file name to create, e.g. 'analysis.md'. The extension must be an allowed type.")] string fileName,
        [Description("The plain-text content to save in the file.")] string content,
        CancellationToken ct = default)
    {
        if (runContext.UserId is not { } userId)
        {
            return WorkspaceToolScope.NoUserError;
        }

        if (string.IsNullOrWhiteSpace(workspace) || string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(content))
        {
            return "Error: workspace (name or id), fileName, and content are required.";
        }

        var extension = Path.GetExtension(fileName);
        if (!AllowedFileTypes.IsAllowed(extension))
        {
            return $"Error: File type '{extension}' is not allowed. Allowed types: {AllowedFileTypes.AllowedList}.";
        }

        try
        {
            var workspaceId = await WorkspaceToolScope.ResolveWorkspaceIdAsync(dbContext, userId, workspace, ct);
            if (workspaceId is null)
            {
                return $"Error: No workspace found with name or ID '{workspace.Trim()}'.";
            }

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
            var blobName = await fileService.UploadToWorkspaceAsync(stream, fileName, workspaceId, ct);

            // Owner is set explicitly: the DbContext only stamps ownership for HTTP users, not for system-scoped workflow runs.
            var projectFile = new ProjectFile
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                FileName = fileName,
                BlobName = blobName,
                Extension = extension,
                SizeBytes = stream.Length,
                WorkspaceId = workspaceId,
                ProcessingStatus = FileProcessingStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            dbContext.ProjectFiles.Add(projectFile);
            await dbContext.SaveChangesAsync(ct);

            ingestionQueue.Enqueue(new FileIngestionRequest(projectFile.Id, projectFile.WorkspaceId));

            return $"Success: File '{fileName}' saved with ID {projectFile.Id}.";
        }
        catch (Exception ex)
        {
            return $"Error: Failed to write file: {ex.Message}";
        }
    }
}
