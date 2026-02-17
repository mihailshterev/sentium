using AgentRuntime.Core.Tools;
using Microsoft.Extensions.AI;

namespace AgentRuntime.Infrastructure.Tools;

public sealed class AgentToolProvider(IEnumerable<IAgentTool> Tools) : IAgentToolProvider
{
    public List<AITool> GetToolsForAgent(string agentName, CancellationToken ct)
    {
        return Tools
            .Where(tool =>
            {
                var policy = ToolPolicyReader.GetPolicy(tool);

                return policy.AllowedAgents.Length == 0 || policy.AllowedAgents.Contains(agentName);
            })
            .Select(t => AIFunctionAdapter.ToAIFunction(t, ct))
            .ToList();
    }
}
