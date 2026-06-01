using Sentium.AgentRuntime.Core.Skills;
using Sentium.AgentRuntime.Infrastructure.Skills.BuiltIn;
using Microsoft.Agents.AI;

namespace Sentium.AgentRuntime.Infrastructure.Skills;

/// <summary>
/// Builds an <see cref="AgentSkillsProvider"/> by combining:
/// 1. Built-in class-based skills (always included)
/// 2. User-created custom skills (persisted in the database)
/// 3. Uploaded markdown file skills (persisted in the database)
///
/// Instantiated per-request so that it always reflects the latest DB state.
/// </summary>
public sealed class DynamicSkillsProvider(IAgentSkillRepository skillRepository, IBuiltInSkillCatalog builtInCatalog)
{
    public async Task<AgentSkillsProvider> BuildAsync(CancellationToken ct = default)
    {
        var dbSkills = await skillRepository.GetAllAsync(ct);

        var builder = new AgentSkillsProviderBuilder();

        BuiltInSkillCatalog.RegisterAll(builder);

        foreach (var skill in dbSkills)
        {
            var inlineSkill = new AgentInlineSkill(
                name: skill.Name,
                description: skill.Description,
                instructions: skill.Instructions);

            builder.UseSkill(inlineSkill);
        }

        return builder.Build();
    }

    public async Task<IReadOnlyList<(string Name, string Description)>> GetCatalogAsync(CancellationToken ct = default)
    {
        var entries = new List<(string Name, string Description)>();

        foreach (var info in builtInCatalog.GetAll())
        {
            entries.Add((info.Name, info.Description));
        }

        var dbSkills = await skillRepository.GetAllAsync(ct);
        foreach (var skill in dbSkills)
        {
            entries.Add((skill.Name, skill.Description));
        }

        return entries;
    }
}
