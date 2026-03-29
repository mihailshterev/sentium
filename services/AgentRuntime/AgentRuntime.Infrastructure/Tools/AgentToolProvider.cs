using System.Collections.Frozen;
using AgentRuntime.Core.Tools;
using Microsoft.Extensions.AI;

namespace AgentRuntime.Infrastructure.Tools;

public sealed class AgentToolProvider : IAgentToolProvider
{
    private readonly FrozenDictionary<string, List<IAgentTool>> toolCache;

    public AgentToolProvider(IEnumerable<IAgentTool> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);

        var tempMap = new Dictionary<string, List<IAgentTool>>(StringComparer.OrdinalIgnoreCase);

        foreach (var tool in tools)
        {
            var policy = ToolPolicyReader.GetPolicy(tool);

            if (policy.AllowedAgents.Length == 0)
            {
                AddToolToMap(tempMap, "*", tool);
            }
            else
            {
                foreach (var agent in policy.AllowedAgents)
                {
                    AddToolToMap(tempMap, agent, tool);
                }
            }
        }

        toolCache = tempMap.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    public List<AITool> GetToolsForAgent(string agentName, CancellationToken ct)
    {
        var result = new List<IAgentTool>();

        if (toolCache.TryGetValue("*", out var globalTools))
        {
            result.AddRange(globalTools);
        }

        if (toolCache.TryGetValue(agentName, out var specificTools))
        {
            result.AddRange(specificTools);
        }

        return result
            .Select(t => AIFunctionAdapter.ToAIFunction(t, ct))
            .ToList();
    }

    private static void AddToolToMap(Dictionary<string, List<IAgentTool>> map, string key, IAgentTool tool)
    {
        if (!map.TryGetValue(key, out var list))
        {
            list = [];
            map[key] = list;
        }

        list.Add(tool);
    }
}
