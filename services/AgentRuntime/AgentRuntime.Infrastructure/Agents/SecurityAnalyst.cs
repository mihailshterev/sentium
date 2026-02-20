using AgentRuntime.Core.Agents;
using AgentRuntime.Core.Tools;

namespace AgentRuntime.Infrastructure.Agents;

public sealed class SecurityAnalyst : IAgent
{
    public string Name => "Security Analyst";
    public string Instructions => "You are a security analyst specialized in network traffic analysis.";
    public IEnumerable<IAgentTool> Tools => Array.Empty<IAgentTool>();
}
