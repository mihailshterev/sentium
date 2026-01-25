using AgentRuntime.Core.Tools;

namespace AgentRuntime.Infrastructure.Tools;

public sealed class FileReadTool : IAgentTool
{
    public string Name => "File Read Tool";

    public string Description => "Reads the contents of a file given its path.";

    public Task<string> ExecuteAsync(string input, CancellationToken ct)
    {
        var fileContent = $"Contents of the file at path: {input}";
        return Task.FromResult(fileContent);
    }
}