namespace Sentium.Praxis.Features.Agents;

/// <param name="Tools">Names of the tools the agent may use; <see langword="null"/> (the default) = every tool.</param>
internal sealed record UpdateAgentRequest(
    Guid Id,
    string Name,
    string Description,
    string Model = "",
    IReadOnlyList<string>? Tools = null);
