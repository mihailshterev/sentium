namespace Sentium.Praxis.Features.Skills;

public sealed record CreateAgentSkillRequest(
    string Name,
    string Description,
    string Instructions,
    AgentSkillType SkillType,
    string? FileName = null);
