using Sentium.Praxis.Contracts;


namespace Sentium.Praxis.Features.Tools;

/// <summary>
/// The registered tools, described for assignment to agents.
/// </summary>
internal interface IToolCatalog
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
