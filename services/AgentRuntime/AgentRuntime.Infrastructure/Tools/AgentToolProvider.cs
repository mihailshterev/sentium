using System.Collections.Frozen;
using AgentRuntime.Core.Tools;
using Microsoft.Extensions.AI;

namespace AgentRuntime.Infrastructure.Tools;

public sealed class AgentToolProvider : IAgentToolProvider
{
    private readonly FrozenDictionary<string, List<AITool>> toolCache;

    public AgentToolProvider(IEnumerable<IAgentTool> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);

        var tempMap = new Dictionary<string, List<AITool>>(StringComparer.OrdinalIgnoreCase);

        foreach (var tool in tools)
        {
            var aiTool = AIFunctionAdapter.ToAIFunction(tool);
            var policy = ToolPolicyReader.GetPolicy(tool);

            if (policy.AllowedAgents.Length == 0)
            {
                AddToolToMap(tempMap, "*", aiTool);
            }
            else
            {
                foreach (var agent in policy.AllowedAgents)
                {
                    AddToolToMap(tempMap, agent, aiTool);
                }
            }
        }

        toolCache = tempMap.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    public List<AITool> GetToolsForAgent(string agentName, CancellationToken ct)
    {
        var hasGlobal = toolCache.TryGetValue("*", out var globalTools);
        var hasSpecific = toolCache.TryGetValue(agentName, out var specificTools);

        var capacity = (hasGlobal ? globalTools!.Count : 0) + (hasSpecific ? specificTools!.Count : 0);

        var result = new List<AITool>(capacity);

        if (hasGlobal)
        {
            result.AddRange(globalTools!);
        }

        if (hasSpecific)
        {
            result.AddRange(specificTools!);
        }

        return result;
    }

    private static void AddToolToMap(Dictionary<string, List<AITool>> map, string key, AITool tool)
    {
        if (!map.TryGetValue(key, out var list))
        {
            list = [];
            map[key] = list;
        }

        list.Add(tool);
    }
}
