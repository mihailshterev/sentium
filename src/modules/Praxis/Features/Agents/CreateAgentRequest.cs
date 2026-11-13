namespace Sentium.Praxis.Features.Agents;

/// <param name="Tools">Names of the tools the agent may use; <see langword="null"/> (the default) = every tool.</param>
public sealed record CreateAgentRequest(
    string Name,
    string Description,
    string Model = "",
    IReadOnlyList<string>? Tools = null);
