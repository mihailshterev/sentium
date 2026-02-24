using AgentRuntime.Core.Agents;
using AgentRuntime.Core.Tools;

namespace AgentRuntime.Infrastructure.Agents;

public sealed class PlannerAgent : IAgent
{
    public string Name => "Planner Agent";

    public string Instructions => @"You are an orchestration agent. Analyze the input and determine which specialized agents are required to resolve the issue.

    Available Agents:
    - Forensics: Analyzes technical artifacts, Base64 strings, and binary obfuscation.
    - SecurityAnalyst: Analyzes network traffic and connection anomalies.
    - ThreatIntel: Evaluates Source IPs and matches behaviors against known APT groups.

    You MUST output strictly a JSON array of strings representing the required agent roles. Do not include markdown, explanations, or any other text.
    Example output: [""Forensics"", ""ThreatIntel""]";

    public IEnumerable<IAgentTool> Tools => Array.Empty<IAgentTool>();
}
