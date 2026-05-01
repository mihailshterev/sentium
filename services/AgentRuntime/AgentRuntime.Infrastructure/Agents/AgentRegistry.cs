using System.Collections.Frozen;
using AgentRuntime.Core.Agents;

namespace AgentRuntime.Infrastructure.Agents;

public sealed class AgentRegistry : IAgentRegistry
{
    private readonly FrozenDictionary<string, Type> agentCache;
    private readonly FrozenDictionary<string, string> instructionCache;

    public AgentRegistry(IEnumerable<IAgent> systemAgents)
    {
        ArgumentNullException.ThrowIfNull(systemAgents);

        var tempMap = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        var tempInstructionMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var agent in systemAgents)
        {
            var name = agent.Name;
            tempMap[name] = agent.GetType();
            tempInstructionMap[name] = agent.Instructions;
        }

        agentCache = tempMap.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        instructionCache = tempInstructionMap.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    public IEnumerable<string> GetRegisteredNames() => agentCache.Keys;

    public string GetInstructions(string name) => instructionCache.GetValueOrDefault(name) ?? "Specialized security agent.";

    public Type? GetAgentType(string name) => agentCache.GetValueOrDefault(name);
}
