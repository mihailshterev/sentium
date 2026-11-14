namespace Sentium.Praxis.Features.Skills.BuiltIn;

/// <summary>
/// Exposes a built-in skill's catalogue entry for the REST API.
/// </summary>
internal interface IBuiltInSkill
{
    BuiltInSkillInfo Describe();
}
