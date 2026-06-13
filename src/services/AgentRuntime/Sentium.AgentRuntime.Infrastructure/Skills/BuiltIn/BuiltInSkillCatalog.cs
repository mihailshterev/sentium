using Sentium.AgentRuntime.Core.Skills;
using Microsoft.Agents.AI;

namespace Sentium.AgentRuntime.Infrastructure.Skills.BuiltIn;

/// <summary>
/// Central registry for all class-based (built-in) <see cref="AgentClassSkill{T}"/> skills.
/// Exposes metadata for the REST API and provides a registration hook for
/// <see cref="AgentSkillsProviderBuilder"/>.
/// </summary>
public sealed class BuiltInSkillCatalog : IBuiltInSkillCatalog
{
    private static readonly IReadOnlyList<BuiltInSkillInfo> Descriptors =
    [
        UnitConverterSkill.Descriptor,
        DateTimeSkill.Descriptor,
        SecurityBestPracticesSkill.Descriptor,
        JsonAnalyzerSkill.Descriptor,
        WritingStyleSkill.Descriptor,
        RegexToolkitSkill.Descriptor,
        CsvAnalyzerSkill.Descriptor,
    ];

    public IReadOnlyList<BuiltInSkillInfo> GetAll() => Descriptors;

    /// <summary>
    /// Registers all built-in skills into the provided <see cref="AgentSkillsProviderBuilder"/>.
    /// </summary>
    public static AgentSkillsProviderBuilder RegisterAll(AgentSkillsProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder
            .UseSkill(new UnitConverterSkill())
            .UseSkill(new DateTimeSkill())
            .UseSkill(new SecurityBestPracticesSkill())
            .UseSkill(new JsonAnalyzerSkill())
            .UseSkill(new WritingStyleSkill())
            .UseSkill(new RegexToolkitSkill())
            .UseSkill(new CsvAnalyzerSkill());
    }
}
