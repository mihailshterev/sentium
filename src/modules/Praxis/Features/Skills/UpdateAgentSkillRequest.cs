namespace Sentium.Praxis.Features.Skills;

internal sealed record UpdateAgentSkillRequest(
    string Name,
    string Description,
    string Instructions);
