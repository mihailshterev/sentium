using AgentRuntime.Core.Agents;
using AgentRuntime.Core.Tools;

namespace AgentRuntime.Infrastructure.Agents;

public sealed class SummaryAgent : IAgent
{
    public string Name => "Summary Agent";
    public string Instructions => "You are a helpful assistant that summarizes text concisely.";
    public IEnumerable<IAgentTool> Tools => Array.Empty<IAgentTool>();
}
