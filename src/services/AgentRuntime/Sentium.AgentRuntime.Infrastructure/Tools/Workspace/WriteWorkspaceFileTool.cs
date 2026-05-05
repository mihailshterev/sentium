using Sentium.AgentRuntime.Core.Entities;
using Sentium.AgentRuntime.Core.Files;
using Sentium.AgentRuntime.Core.Storage;
using Sentium.AgentRuntime.Core.Tools;
using Sentium.AgentRuntime.Core.Tools.Attributes;
using Sentium.AgentRuntime.Infrastructure.Data;
using Sentium.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace Sentium.AgentRuntime.Infrastructure.Tools.Workspace;

/// <summary>
/// An agent tool that saves a new text file to a workspace.
/// </summary>
/// <remarks>
/// <para>
/// This tool is marked as <see cref="ToolRiskLevel.Medium"/> risk because it creates new resources
/// in cloud storage and the database.
/// </para>
/// <para>
/// Input format: A JSON object with three required properties:
/// - <c>workspace</c>: Workspace ID (GUID) or workspace name (case-insensitive).
/// - <c>fileName</c>: The desired filename (e.g., "analysis.md"). Extension must be allowed (see <see cref="AllowedFileTypes"/>).
/// - <c>content</c>: The plain-text file content to save.
/// </para>
/// <para>
/// Example input:
/// <c>{"input": "{\"workspace\": \"my-project\", \"fileName\": \"notes.md\", \"content\": \"# Project Notes\\n...\"}"}</c>
/// </para>
/// <para>
/// Output: Success message with the file ID, or an error message if validation or upload fails.
/// After successful save, the file is automatically queued for RAG ingestion.
/// </para>
/// </remarks>
[AgentToolPolicy(RiskLevel = ToolRiskLevel.Medium)]
public sealed class WriteWorkspaceFileTool(AgentRuntimeDbContext dbContext, ILocalFileService fileService, IEventBus eventBus) : IAgentTool
{
    private readonly JsonSerializerOptions options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <inheritdoc/>
    public string Name => "write_workspace_file";

    /// <inheritdoc/>
    public string Description => "Saves a new text file to a workspace. " +
                                 "Accepts workspace ID (GUID) or workspace name. " +
                                 "Call this tool with: {\"input\": \"{\\\"workspace\\\": \\\"name-or-guid\\\", \\\"fileName\\\": \\\"example.md\\\", \\\"content\\\": \\\"text content\\\"}\"}";

    /// <inheritdoc/>
    public async Task<string> ExecuteAsync(string input, CancellationToken ct)
    {
        try
        {
            var request = JsonSerializer.Deserialize<WriteRequest>(input, options);
            if (request is null || string.IsNullOrWhiteSpace(request.FileName) || string.IsNullOrWhiteSpace(request.Content) || string.IsNullOrWhiteSpace(request.Workspace))
            {
                return "Error: workspace (name or id), fileName, and content are required.";
            }

            var extension = Path.GetExtension(request.FileName);
            if (!AllowedFileTypes.IsAllowed(extension))
            {
                return $"Error: File type '{extension}' is not allowed. Allowed types: {AllowedFileTypes.AllowedList}.";
            }

            // Resolve workspace by GUID or name
            Guid? workspaceId = null;
            if (Guid.TryParse(request.Workspace, out var wsGuid))
            {
                var exists = await dbContext.Workspaces.AnyAsync(w => w.Id == wsGuid, ct);
                workspaceId = exists ? wsGuid : null;
            }
            else
            {
                var workspace = await dbContext.Workspaces
                    .Where(w => w.Name.ToLower() == request.Workspace.ToLower())
                    .Select(w => new { w.Id })
                    .FirstOrDefaultAsync(ct);
                workspaceId = workspace?.Id;
            }

            if (workspaceId is null)
            {
                return $"Error: No workspace found with name or ID '{request.Workspace}'.";
            }

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(request.Content));
            var blobName = await fileService.UploadToWorkspaceAsync(stream, request.FileName, workspaceId, ct);

            var projectFile = new ProjectFile
            {
                Id = Guid.NewGuid(),
                FileName = request.FileName,
                BlobName = blobName,
                Extension = extension,
                SizeBytes = stream.Length,
                WorkspaceId = workspaceId,
                ProcessingStatus = FileProcessingStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            dbContext.ProjectFiles.Add(projectFile);
            await dbContext.SaveChangesAsync(ct);

            await eventBus.PublishAsync(FileEvents.FileIngested, new FileIngestedEvent(projectFile.Id, projectFile.WorkspaceId), ct: ct);

            return $"Success: File '{request.FileName}' saved with ID {projectFile.Id}.";
        }
        catch (Exception ex)
        {
            return $"Error: Failed to write file: {ex.Message}";
        }
    }

    /// <summary>
    /// Represents a request to write a file to a workspace.
    /// </summary>
    /// <param name="Workspace">The workspace ID (GUID) or name where the file will be saved.</param>
    /// <param name="FileName">The name of the file to create (extension determines file type).</param>
    /// <param name="Content">The plain-text content to save in the file.</param>
    private record WriteRequest(string Workspace, string FileName, string Content);
}
