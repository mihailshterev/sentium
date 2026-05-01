using AgentRuntime.Core.Tools;
using AgentRuntime.Core.Tools.Attributes;
using System.Security;

namespace AgentRuntime.Infrastructure.Tools;

[AgentToolPolicy(
    AllowedAgents = [],
    RiskLevel = ToolRiskLevel.Medium,
    RequiresApproval = false)]
public sealed class ReadFileTool : IAgentTool
{
    private readonly long maxFileSizeBytes = 1024 * 1024 * 2;
    private readonly string workspaceRoot;

    private readonly HashSet<string> allowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".json", ".xml", ".csv", ".log", ".yaml", ".yml",
        ".js", ".ts", ".py", ".cs", ".html", ".css", ".sql"
    };

    public ReadFileTool()
    {
        workspaceRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "user_workspace"));

        if (!Directory.Exists(workspaceRoot))
        {
            Directory.CreateDirectory(workspaceRoot);
        }
    }

    public string Name => "read_file";

    public string Description =>
        "Reads and returns the full text content of a file. " +
        "Input must be a relative path (e.g., 'logs/error.log') within the allowed workspace in this format {\"input\": \"relative/path/to/file\"}.";

    public async Task<string> ExecuteAsync(string input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return "Error: File path cannot be empty.";
        }

        try
        {
            var absolutePath = Path.GetFullPath(Path.Combine(workspaceRoot, input));

            if (!absolutePath.StartsWith(workspaceRoot, StringComparison.OrdinalIgnoreCase))
            {
                return "Security Error: Access denied. You cannot access files outside the workspace.";
            }

            var fileInfo = new FileInfo(absolutePath);

            if (!fileInfo.Exists)
            {
                return $"Error: File '{input}' not found.";
            }

            var extension = fileInfo.Extension;
            if (!allowedExtensions.Contains(extension))
            {
                return $"Error: File type '{extension}' is restricted. Only text-based files can be read.";
            }

            if (fileInfo.Length > maxFileSizeBytes)
            {
                return $"Error: File is too large ({fileInfo.Length / 1024} KB). Max limit is {maxFileSizeBytes / 1024} KB.";
            }

            using var fileStream = new FileStream(
                absolutePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite);

            using var reader = new StreamReader(fileStream);

            var content = await reader.ReadToEndAsync(ct);

            return string.IsNullOrEmpty(content)
                ? "Notice: The file is empty."
                : content;
        }
        catch (SecurityException)
        {
            return "Error: System security prevents reading this file.";
        }
        catch (IOException ex)
        {
            return $"Error: Could not read file due to an I/O issue: {ex.Message}";
        }
        catch (Exception)
        {
            return "Error: An unexpected error occurred while accessing the workspace.";
        }
    }
}
