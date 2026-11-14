namespace Sentium.Praxis.Features.Agents;

/// <summary>
/// Definition of an agent persona: identity, behaviour, and the tools it may use.
/// </summary>
internal interface IAgent
{
    /// <summary>
    /// Unique name; also the key the agent is registered and resolved under.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The system instructions (persona prompt) that define the agent's role and constraints.
    /// </summary>
    string Instructions { get; }

    /// <summary>
    /// One-line summary of what the agent is good at - shown to the squad planner when choosing agents.
    /// </summary>
    string Description => Instructions;

    /// <summary>
    /// Names of the tools this agent may use (see <c>ToolNames</c>), or <see langword="null"/> for every tool. Small
    /// models choose tools far more reliably from a short, role-relevant list.
    /// </summary>
    IReadOnlyList<string>? Tools => null;
}
