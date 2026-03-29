using AgentRuntime.Core.Agents;

namespace AgentRuntime.Infrastructure.Agents;

public sealed class SecurityAnalyst : IAgent
{
    public string Name => "Security Analyst";
    public string Instructions => "You are a security analyst specialized in network traffic analysis.";
}
