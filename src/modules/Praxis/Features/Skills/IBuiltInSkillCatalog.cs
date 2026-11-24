using Microsoft.Agents.AI;

namespace Sentium.Praxis.Features.Skills;

internal interface IBuiltInSkillCatalog
{
    /// <summary>
    /// The REST API view of <see cref="Skills"/>.
    /// </summary>
    IReadOnlyList<BuiltInSkillInfo> GetAll();

    /// <summary>
    /// The shared, stateless skill instances handed to the agent's <see cref="AgentSkillsProvider"/>.
    /// </summary>
    IReadOnlyList<AgentSkill> Skills { get; }
}
