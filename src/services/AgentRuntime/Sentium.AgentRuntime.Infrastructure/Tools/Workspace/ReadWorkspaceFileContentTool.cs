using Sentium.AgentRuntime.Core.Storage;
using Sentium.AgentRuntime.Core.Tools;
using Sentium.AgentRuntime.Core.Tools.Attributes;
using Sentium.AgentRuntime.Infrastructure.Data;

namespace Sentium.AgentRuntime.Infrastructure.Tools.Workspace;

/// <summary>
/// An agent tool that reads and returns the full text content of a workspace file.
/// </summary>
/// <remarks>
/// <para>
/// This tool is marked as <see cref="ToolRiskLevel.Medium"/> risk because it retrieves potentially
/// large file contents from cloud storage.
/// </para>
/// <para>
/// Input format: A file GUID (unique identifier).
/// - Input: <c>{"input": "550e8400-e29b-41d4-a716-446655440000"}</c>
/// </para>
/// <para>
/// Output: The plain-text content of the file. If the file is large, the full content is returned.
/// Agents should be aware of token limits when processing large files.
/// </para>
/// <para>
/// Typical workflow:
/// 1. Use <see cref="ListWorkspacesTool"/> to find workspace names.
/// 2. Use <see cref="ListWorkspaceFilesTool"/> to list files and get file IDs.
/// 3. Use this tool to read the content of a specific file.
/// </para>
/// </remarks>
[AgentToolPolicy(RiskLevel = ToolRiskLevel.Medium)]
public sealed class ReadWorkspaceFileContentTool(AgentRuntimeDbContext dbContext, ILocalFileService fileService) : IAgentTool
{
    /// <inheritdoc/>
    public string Name => "read_file_content";

    /// <inheritdoc/>
    public string Description => "Reads the text content of a specific file from the workspace. " +
                                 "Call this tool in the following format: {\"input\": \"The guid of the file\"}";

    /// <inheritdoc/>
    public async Task<string> ExecuteAsync(string input, CancellationToken ct)
    {
        try
        {
            if (!Guid.TryParse(input, out var guid))
            {
                return "Error: A valid file GUID is required.";
            }

            var projectFile = await dbContext.ProjectFiles.FindAsync([guid], ct);
            if (projectFile == null)
            {
                return $"Error: File with ID {guid} not found.";
            }

            using var stream = await fileService.GetFileStreamAsync(projectFile.BlobName, ct);
            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync(ct);

            return content;
        }
        catch (Exception ex)
        {
            return $"Error: Failed to read file content: {ex.Message}";
        }
    }
}
