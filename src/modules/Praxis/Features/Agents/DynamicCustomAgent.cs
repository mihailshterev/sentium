
namespace Sentium.Praxis.Features.Agents;

public sealed class DynamicCustomAgent(string name, string instructions) : IAgent
{
    public string Name { get; } = name;
    public string Instructions { get; } = instructions;
    public string Description => Instructions;
}
