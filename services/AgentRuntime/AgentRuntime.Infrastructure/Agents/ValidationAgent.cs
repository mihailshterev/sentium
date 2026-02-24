using AgentRuntime.Core.Agents;
using AgentRuntime.Core.Tools;

namespace AgentRuntime.Infrastructure.Agents;

public sealed class ValidationAgent : IAgent
{
    public string Name => "Validator";

    public string Instructions => @"You are a Senior Security Reviewer.
    Your task is to review the findings provided by a squad of specialized agents.

    1. Summarize the collective findings.
    2. Assess the 'Risk Level' (Low, Medium, High, Critical).
    3. Provide a clear 'Final Recommendation'.

    You must output your response in a structured format:
    SUMMARY: [Text]
    RISK: [Level]
    RECOMMENDATION: [Text]";

    public IEnumerable<IAgentTool> Tools => Array.Empty<IAgentTool>();
}
