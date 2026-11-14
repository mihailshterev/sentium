namespace Sentium.Praxis.Features.Skills;

public sealed record UpdateAgentSkillRequest(
    string Name,
    string Description,
    string Instructions);
