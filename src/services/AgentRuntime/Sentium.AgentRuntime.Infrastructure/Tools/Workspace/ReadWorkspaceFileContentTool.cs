using System.Text.Json;
using Sentium.AgentRuntime.Core.Storage;
using Sentium.AgentRuntime.Core.Tools;
using Sentium.AgentRuntime.Core.Tools.Attributes;
using Sentium.AgentRuntime.Infrastructure.Data;

namespace Sentium.AgentRuntime.Infrastructure.Tools.Workspace;

/// <summary>
/// An agent tool that reads and returns the full text content of a workspace file.
/// </summary>
[AgentToolPolicy(RiskLevel = ToolRiskLevel.Medium)]
public sealed class ReadWorkspaceFileContentTool(AgentRuntimeDbContext dbContext, ILocalFileService fileService) : IAgentTool
{
    private static readonly JsonSerializerOptions jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public string Name => "read_file_content";

    public string Description => "Reads the text content of a specific file from the workspace. " +
                                 "Call this tool in the following format: {\"input\": \"The guid of the file\"}";

    public async Task<string> ExecuteAsync(string input, CancellationToken ct)
    {
        try
        {
            var request = JsonSerializer.Deserialize<ReadWorkspaceFileRequest>(input, jsonOptions);
            if (request is null || !Guid.TryParse(request.Input, out var guid))
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

    private sealed record ReadWorkspaceFileRequest(string Input);
}
