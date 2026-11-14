using Microsoft.Agents.AI;
using Sentium.Praxis.Contracts;

namespace Sentium.Praxis.Features.Skills;

/// <summary>
/// Builds the <see cref="AgentSkillsProvider"/> attached to each agent: the shared built-in skills plus the acting
/// user's own skills (<see cref="UserSkillsSource"/>). The provider advertises skill names/descriptions in the system
/// prompt and exposes the <c>load_skill</c> / <c>read_skill_resource</c> / <c>run_skill_script</c> tools.
/// </summary>
/// <remarks>
/// Skill tools default to requiring human approval in Agent Framework. Built-in skills are trusted application code and
/// user skills are instructions-only, so approval is disabled - otherwise every <c>load_skill</c> would surface an
/// approval prompt in chat and stall background workflows that have no one to answer it.
/// </remarks>
public sealed class AgentSkillsProviderFactory(
    IBuiltInSkillCatalog builtInCatalog,
    IAgentSkillRepository skillRepository,
    IPdpContextAccessor runContext,
    ILoggerFactory loggerFactory)
{
    public AgentSkillsProvider Create()
    {
        return new AgentSkillsProviderBuilder()
            .UseLoggerFactory(loggerFactory)
            .UseSkills(builtInCatalog.Skills)
            .UseSource(_ => new UserSkillsSource(skillRepository, runContext))
            .UseOptions(options =>
            {
                options.DisableLoadSkillApproval = true;
                options.DisableReadSkillResourceApproval = true;
                options.DisableRunSkillScriptApproval = true;
            })
            .Build();
    }
}
