using Sentium.AgentRuntime.Core.Agents;

namespace Sentium.AgentRuntime.Infrastructure.Agents;

public sealed class DynamicCustomAgent(string name, string instructions) : IAgent
{
    public string Name { get; } = name;
    public string Instructions { get; } = instructions;
}
