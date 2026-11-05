using System.Collections.Frozen;
using Microsoft.Extensions.AI;

namespace Sentium.Praxis.Features.Tools;

/// <summary>
/// Groups the registered tools by the agents allowed to use them (<see cref="Attributes.AgentToolPolicyAttribute.AllowedAgents"/>).
/// </summary>
public sealed class AgentToolProvider : IAgentToolProvider
{
    private const string AllAgents = "*";

    private readonly FrozenDictionary<string, List<AITool>> toolCache;

    public AgentToolProvider(IEnumerable<IAgentTool> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);

        var map = new Dictionary<string, List<AITool>>(StringComparer.OrdinalIgnoreCase);

        foreach (var tool in tools)
        {
            var function = tool.CreateFunction();
            var policy = ToolPolicyReader.GetPolicy(tool);

            string[] keys = policy.AllowedAgents.Length == 0 ? [AllAgents] : policy.AllowedAgents;
            foreach (var key in keys)
            {
                if (!map.TryGetValue(key, out var list))
                {
                    map[key] = list = [];
                }

                list.Add(function);
            }
        }

        toolCache = map.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    public List<AITool> GetToolsForAgent(string agentName, CancellationToken ct)
    {
        var result = new List<AITool>();

        if (toolCache.TryGetValue(AllAgents, out var globalTools))
        {
            result.AddRange(globalTools);
        }

        if (toolCache.TryGetValue(agentName, out var specificTools))
        {
            result.AddRange(specificTools);
        }

        return result;
    }
}
