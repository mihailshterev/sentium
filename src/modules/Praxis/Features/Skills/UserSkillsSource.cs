using Microsoft.Agents.AI;
using Sentium.Praxis.Contracts;

namespace Sentium.Praxis.Features.Skills;

/// <summary>
/// Agent Framework skill source for the acting user's own skills (custom and uploaded markdown), loaded from the
/// database. Returns nothing when no user is associated with the run (fail closed).
/// </summary>
public sealed class UserSkillsSource(IAgentSkillRepository repository, IPdpContextAccessor runContext) : AgentSkillsSource
{
    public override async Task<IList<AgentSkill>> GetSkillsAsync(AgentSkillsSourceContext context, CancellationToken cancellationToken = default)
    {
        if (runContext.UserId is not { } userId)
        {
            return [];
        }

        var skills = await repository.GetForUserAsync(userId, cancellationToken);

        return [.. skills.Select(s => new AgentInlineSkill(name: s.Name, description: s.Description, instructions: s.Instructions))];
    }
}
