using AgentRuntime.Core.Agents;

namespace AgentRuntime.Application.Agents.Native;

public sealed class SecurityAnalyst : IAgent
{
    public string Name => "Security Analyst";
    public string Instructions => "You are a security analyst specialized in network traffic analysis.";
}
