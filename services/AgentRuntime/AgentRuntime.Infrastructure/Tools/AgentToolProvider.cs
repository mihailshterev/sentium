using AgentRuntime.Core.Tools;

namespace AgentRuntime.Infrastructure.Tools;

public sealed class AgentToolProvider
{
    private readonly IToolRegistry ToolRegistry;

    public AgentToolProvider(IToolRegistry registry)
    {
        ToolRegistry = registry;
    }

    public IReadOnlyList<IAgentTool> GetToolsForAgent(string agentName)
    {
        return ToolRegistry.GetTools()
            .Where(tool =>
            {
                var policy = ToolPolicyReader.GetPolicy(tool);

                if (policy.AllowedAgents.Length > 0 && !policy.AllowedAgents.Contains(agentName))
                {
                    return false;
                }

                return true;
            })
            .ToList();
    }
}
