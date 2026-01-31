using AgentRuntime.Core.Tools;
using Microsoft.Extensions.AI;

namespace AgentRuntime.Infrastructure.Tools;

public sealed class AgentToolProvider
{
    public static List<AITool> GetToolsForAgent(string agentName, CancellationToken ct)
    {
        var tools = new List<AITool>();

        var types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => typeof(IAgentTool).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        foreach (var type in types)
        {
            var instance = (IAgentTool)Activator.CreateInstance(type)!;
            var policy = ToolPolicyReader.GetPolicy(instance);

            if (policy.AllowedAgents.Length == 0 || !policy.AllowedAgents.Contains(agentName))
            {
                continue;
            }

            tools.Add(AIFunctionAdapter.ToAIFunction(instance, ct));
        }

        return tools;
    }
}
