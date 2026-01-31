using AgentRuntime.Core.Agents;

namespace AgentRuntime.Infrastructure.Agents;

public sealed class AgentRegistry : IAgentRegistry
{
    private readonly Dictionary<string, Type> Registry;

    public AgentRegistry()
    {
        Registry = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
        {
            { "Planner", typeof(PlannerAgent) },
        };
    }

    public Type? GetAgentType(string name)
    {
        return Registry.TryGetValue(name, out var type) ? type : null;
    }

    public IEnumerable<string> GetRegisteredNames() => Registry.Keys;
}
