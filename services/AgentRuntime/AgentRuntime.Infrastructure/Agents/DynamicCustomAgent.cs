using AgentRuntime.Core.Agents;

namespace AgentRuntime.Infrastructure.Agents;

public sealed class DynamicCustomAgent(string name, string instructions) : IAgent
{
    public string Name { get; } = name;
    public string Instructions { get; } = instructions;
}
