namespace AgentRuntime.Core.Tools;

public interface IAgentTool
{
    string Name { get; }
    string Description { get; }
    Task<string> ExecuteAsync(string input, CancellationToken ct);
}