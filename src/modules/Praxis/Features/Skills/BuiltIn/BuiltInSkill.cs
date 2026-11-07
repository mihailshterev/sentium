using Microsoft.Agents.AI;

namespace Sentium.Praxis.Features.Skills.BuiltIn;

/// <summary>
/// Exposes a built-in skill's catalogue entry for the REST API.
/// </summary>
internal interface IBuiltInSkill
{
    BuiltInSkillInfo Describe();
}

/// <summary>
/// Base for the class-based built-in skills. The name, description and instructions are declared once and both the
/// Agent Framework frontmatter and the REST catalogue entry are derived from them, so the two can never drift.
/// Scripts and resources stay on the derived class as <see cref="AgentSkillScriptAttribute"/> /
/// <see cref="AgentSkillResourceAttribute"/> members.
/// </summary>
internal abstract class BuiltInSkill<TSelf>(string name, string description, string instructions) : AgentClassSkill<TSelf>, IBuiltInSkill where TSelf : BuiltInSkill<TSelf>
{
    public override AgentSkillFrontmatter Frontmatter { get; } = new(name, description);

    protected override string Instructions => instructions;

    public BuiltInSkillInfo Describe() => new(name, description, instructions);
}
