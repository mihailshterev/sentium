using Microsoft.Agents.AI;

namespace Sentium.Praxis.Features.Skills;

/// <summary>
/// Provides the class-based (built-in) skills registered in the system.
/// </summary>
internal interface IBuiltInSkillCatalog
{
    /// <summary>
    /// Returns the full list of built-in skill descriptors (REST API view).
    /// </summary>
    IReadOnlyList<BuiltInSkillInfo> GetAll();

    /// <summary>
    /// The shared, stateless skill instances handed to the agent's <see cref="AgentSkillsProvider"/>.
    /// </summary>
    IReadOnlyList<AgentSkill> Skills { get; }
}
