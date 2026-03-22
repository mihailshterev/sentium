using System.Collections.Frozen;
using AgentRuntime.Core.Agents;

namespace AgentRuntime.Infrastructure.Agents;

public sealed class AgentRegistry : IAgentRegistry
{
    private readonly FrozenDictionary<string, Type> Registry;

    public AgentRegistry()
    {
        Registry = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
        {
            { AgentRole.Planner, typeof(PlannerAgent) },
            { AgentRole.SecurityAnalyst, typeof(SecurityAnalyst) },
            { AgentRole.Summarizer, typeof(SummaryAgent) },
            { AgentRole.ThreatIntel, typeof(ThreatIntelAgent) },
            { AgentRole.Forensics, typeof(ForensicsAgent) },
            { AgentRole.Validator, typeof(ValidationAgent) }
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    public Type? GetAgentType(string name) => Registry.TryGetValue(name, out var type) ? type : null;

    public IEnumerable<string> GetRegisteredNames() => Registry.Keys;
}
