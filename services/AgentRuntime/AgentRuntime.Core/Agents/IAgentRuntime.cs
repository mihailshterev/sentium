namespace AgentRuntime.Core.Agents;

public interface IAgentRuntime
{
    Task<string> RunAsync(string systemPrompt, string input, CancellationToken ct);
}
