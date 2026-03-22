using AgentRuntime.Core.Tools;

namespace AgentRuntime.Infrastructure.Tools;

public sealed class FileReadTool : IAgentTool
{
    public string Name => "read_file";

    public string Description =>
        "Reads and returns the full text content of a file from the local file system. " +
        "Input must be a valid absolute or relative file path.";

    public async Task<string> ExecuteAsync(string filePath, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        try
        {
            if (!File.Exists(filePath))
            {
                return $"Error: The file at '{filePath}' does not exist.";
            }

            using var reader = new StreamReader(filePath);
            var content = await reader.ReadToEndAsync(ct);

            return content;
        }
        catch (UnauthorizedAccessException)
        {
            return $"Error: Access denied to file '{filePath}'. Permission issues.";
        }
        catch (Exception ex)
        {
            return $"Error: An unexpected error occurred: {ex.Message}";
        }
    }
}
