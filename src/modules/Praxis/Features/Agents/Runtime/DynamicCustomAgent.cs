
namespace Sentium.Praxis.Features.Agents.Runtime;

/// <summary>
/// A user-defined agent: its description doubles as the persona prompt, and <paramref name="tools"/> (<see langword="null"/>
/// = every tool) is the tool list chosen in the portal.
/// </summary>
internal sealed class DynamicCustomAgent(string name, string instructions, IReadOnlyList<string>? tools = null) : IAgent
{
    public string Name { get; } = name;
    public string Instructions { get; } = instructions;
    public string Description => Instructions;
    public IReadOnlyList<string>? Tools { get; } = tools;
}
