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
    public string Name => "read_file_content";

    public string Description => "Reads the text content of a specific file from the workspace, identified by its GUID.";

    public IReadOnlyList<AgentToolParameter> Parameters { get; } = [new("fileId", "The GUID of the file to read (from list_workspace_files).")];

    public async Task<string> ExecuteAsync(string input, CancellationToken ct)
    {
        try
        {
            if (!TryExtractGuid(input, out var guid))
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

    private static bool TryExtractGuid(string input, out Guid guid)
    {
        guid = Guid.Empty;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var trimmed = input.Trim();
        if (Guid.TryParse(trimmed, out guid))
        {
            return true;
        }

        if (!trimmed.StartsWith('{'))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            foreach (var key in GuidKeys)
            {
                if (doc.RootElement.TryGetProperty(key, out var value)
                    && value.ValueKind == JsonValueKind.String
                    && Guid.TryParse(value.GetString(), out guid))
                {
                    return true;
                }
            }
        }
        catch (JsonException)
        {
            // Not JSON after all - fall through to failure.
        }

        return false;
    }

    private static readonly string[] GuidKeys = ["input", "fileId", "id", "guid", "fileGuid"];
}
