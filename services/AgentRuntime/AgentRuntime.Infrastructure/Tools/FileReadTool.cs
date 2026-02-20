using AgentRuntime.Core.Tools;
using AgentRuntime.Core.Tools.Attributes;

namespace AgentRuntime.Infrastructure.Tools;

[AgentToolPolicy(
    AllowedAgents = new[] { "FileReaderAgent" },
    RiskLevel = ToolRiskLevel.Low,
    RequiresApproval = false)]
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
