using System.Collections.Frozen;
using AgentRuntime.Core.Agents;

namespace AgentRuntime.Infrastructure.Agents;

public sealed class AgentRegistry : IAgentRegistry
{
    private readonly FrozenDictionary<string, Type> Registry;
    private readonly FrozenDictionary<string, string> Descriptions;

    public AgentRegistry()
    {
        var dict = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
        {
            { AgentRole.Planner, typeof(PlannerAgent) },
            { AgentRole.SecurityAnalyst, typeof(SecurityAnalyst) },
            { AgentRole.Summarizer, typeof(SummaryAgent) },
            { AgentRole.ThreatIntel, typeof(ThreatIntelAgent) },
            { AgentRole.Forensics, typeof(ForensicsAgent) },
            { AgentRole.Validator, typeof(ValidationAgent) }
        };

        Registry = dict.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

        Descriptions = dict.ToDictionary(
            kvp => kvp.Key,
            kvp => GetInstructionsFromType(kvp.Value),
            StringComparer.OrdinalIgnoreCase
        ).ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    private static string GetInstructionsFromType(Type type)
    {
        var instance = Activator.CreateInstance(type) as IAgent;
        return instance?.Instructions ?? "Specialized security agent.";
    }

    public string GetInstructions(string name) => Descriptions.TryGetValue(name, out var desc) ? desc : "Unknown agent.";

    public IEnumerable<string> GetRegisteredNames() => Registry.Keys;

    public Type? GetAgentType(string name) => Registry.TryGetValue(name, out var type) ? type : null;
}
