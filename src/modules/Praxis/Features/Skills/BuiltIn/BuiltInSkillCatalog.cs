using Microsoft.Agents.AI;

namespace Sentium.Praxis.Features.Skills.BuiltIn;

/// <summary>
/// Central registry for all class-based (built-in) skills. Holds one shared, stateless instance of each skill for the
/// <see cref="AgentSkillsProvider"/> and derives the REST API descriptors from those same instances.
/// </summary>
internal sealed class BuiltInSkillCatalog : IBuiltInSkillCatalog
{
    private static readonly IReadOnlyList<AgentSkill> SkillInstances =
    [
        new UnitConverterSkill(),
        new DateTimeSkill(),
        new SecurityBestPracticesSkill(),
        new JsonAnalyzerSkill(),
        new WritingStyleSkill(),
        new RegexToolkitSkill(),
        new CsvAnalyzerSkill(),
    ];

    private static readonly IReadOnlyList<BuiltInSkillInfo> Descriptors =
        [.. SkillInstances.OfType<IBuiltInSkill>().Select(s => s.Describe())];

    public IReadOnlyList<BuiltInSkillInfo> GetAll() => Descriptors;

    public IReadOnlyList<AgentSkill> Skills => SkillInstances;
}
