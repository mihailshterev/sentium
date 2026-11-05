using Sentium.Praxis.Entities;

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

public sealed record CreateAgentSkillRequest(
    string Name,
    string Description,
    string Instructions,
    AgentSkillType SkillType,
    string? FileName = null);

public sealed record UpdateAgentSkillRequest(
    string Name,
    string Description,
    string Instructions);

public sealed record BuiltInSkillInfo(
    string Name,
    string Description,
    string Instructions);
