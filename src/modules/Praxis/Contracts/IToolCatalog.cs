namespace Sentium.Praxis.Contracts;

/// <summary>
/// The registered tools, described for assignment to agents. Built-in tools and the approved tools of every
/// <see cref="IAgentToolSource"/> are both listed.
/// </summary>
public interface IToolCatalog
{
    /// <summary>
    /// Every registered tool, ordered by resource then name.
    /// </summary>
    IReadOnlyList<ToolDescriptor> GetTools();

    /// <summary>
    /// Whether a tool named <paramref name="name"/> (case-insensitive) is registered.
    /// </summary>
    bool Contains(string name);
}
