using AgentRuntime.Core.Agents;

namespace AgentRuntime.Infrastructure.Agents;

public sealed class SummaryAgent : IAgent
{
    public string Name => "Summary Agent";
    public string Instructions => "You are a helpful assistant that summarizes text concisely.";
}
