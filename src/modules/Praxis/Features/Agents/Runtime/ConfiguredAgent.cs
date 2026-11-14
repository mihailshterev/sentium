namespace Sentium.Praxis.Features.Agents.Runtime;

/// <summary>
/// A built-in agent whose tool list the user replaced; everything else comes from <paramref name="inner"/>.
/// </summary>
internal sealed class ConfiguredAgent(IAgent inner, IReadOnlyList<string>? tools) : IAgent
{
    public string Name => inner.Name;
    public string Instructions => inner.Instructions;
    public string Description => inner.Description;
    public IReadOnlyList<string>? Tools { get; } = tools;
}
