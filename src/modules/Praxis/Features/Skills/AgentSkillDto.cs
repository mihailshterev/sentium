namespace Sentium.Praxis.Features.Skills;

public sealed record AgentSkillDto(
    Guid Id,
    string Name,
    string Description,
    string Instructions,
    AgentSkillType SkillType,
    string? FileName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
