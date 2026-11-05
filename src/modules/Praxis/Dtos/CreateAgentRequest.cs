namespace Sentium.Praxis.Dtos;

public sealed record CreateAgentRequest(
    string Name,
    string Description,
    string Model = "");
