using Sentium.AgentRuntime.Core.Tools;
using Sentium.AgentRuntime.Core.Tools.Attributes;
using System.Security;

namespace Sentium.AgentRuntime.Infrastructure.Tools;

/// <summary>
/// An agent tool that reads and returns the full text content of files from a local workspace directory.
/// </summary>
/// <remarks>
/// <para>
/// This tool is marked as <see cref="ToolRiskLevel.Medium"/> risk and enforces strict security:
/// - Only files within the <c>user_workspace</c> directory can be accessed (path traversal prevention).
/// - Only text-based file extensions are allowed (see <see cref="allowedExtensions"/>).
/// - File size is limited to 2 MB to prevent memory exhaustion.
/// </para>
/// <para>
/// Input format: A relative file path within the workspace.
/// - Input: <c>{"input": "logs/error.log"}</c>
/// - Input: <c>{"input": "config.json"}</c>
/// </para>
/// <para>
/// Output: The full plain-text content of the file, or an error message if validation fails.
/// </para>
/// <para>
/// Supported file extensions: .txt, .md, .json, .xml, .csv, .log, .yaml, .yml, .js, .ts, .py, .cs, .html, .css, .sql
/// </para>
/// </remarks>
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

    /// <summary>
    /// Initializes the read file tool and creates the workspace directory if it does not exist.
    /// </summary>
    public ReadFileTool()
    {
        workspaceRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "user_workspace"));

        if (!Directory.Exists(workspaceRoot))
        {
            Directory.CreateDirectory(workspaceRoot);
        }
    }

    /// <inheritdoc/>
    public string Name => "read_file";

    /// <inheritdoc/>
    public string Description =>
        "Reads and returns the full text content of a file. " +
        "Input must be a relative path (e.g., 'logs/error.log') within the allowed workspace in this format {\"input\": \"relative/path/to/file\"}.";

    /// <inheritdoc/>
    public async Task<string> ExecuteAsync(string input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return "Error: File path cannot be empty.";
        }

        try
        {
            var absolutePath = Path.GetFullPath(Path.Combine(workspaceRoot, input));

            // Security check: prevent path traversal attacks
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

            return string.IsNullOrEmpty(content) ? "Notice: The file is empty." : content;
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
