using AgentRuntime.Core.Agents;
using AgentRuntime.Core.Tools;

namespace AgentRuntime.Infrastructure.Agents;

public sealed class PlannerAgent : IAgent
{
    public string Name => "Planner Agent";
    public string Instructions => $"You are an orchestrator. Based on the input, delegate tasks to the appropriate agents.";
    public IEnumerable<IAgentTool> Tools => Array.Empty<IAgentTool>();
}
