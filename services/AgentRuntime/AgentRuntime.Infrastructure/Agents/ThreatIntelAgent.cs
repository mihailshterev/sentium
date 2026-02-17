using AgentRuntime.Core.Agents;
using AgentRuntime.Core.Tools;

namespace AgentRuntime.Infrastructure.Agents;

public sealed class ThreatIntelAgent : IAgent
{
    public string Name => "Threat Intel";
    public string Instructions => @"You are a Threat Intelligence specialist.
        Your job is to:
        1. Evaluate the Source IP and User context against known TTPs (Tactics, Techniques, and Procedures).
        2. Assign a 'Confidence Score' to the threat.
        3. Identify if the behavior matches known APT group signatures (e.g., Lazarus, Fancy Bear).";
    public IEnumerable<IAgentTool> Tools => Array.Empty<IAgentTool>();
}
