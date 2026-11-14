using System.Collections.Frozen;

namespace Sentium.Praxis.Features.Agents.Runtime;

public sealed class AgentRegistry : IAgentRegistry
{
    private readonly FrozenSet<string> names;

    public AgentRegistry(IEnumerable<IAgent> nativeAgents)
    {
        ArgumentNullException.ThrowIfNull(nativeAgents);
        names = nativeAgents.Select(a => a.Name).ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    }

    public IEnumerable<string> GetRegisteredNames() => names;
}
