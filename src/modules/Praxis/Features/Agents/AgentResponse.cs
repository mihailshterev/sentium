namespace Sentium.Praxis.Features.Agents;

/// <param name="Tools">Names of the tools the agent may use; <see langword="null"/> = every tool.</param>
public sealed record AgentResponse(
    Guid Id,
    string Name,
    string Description,
    string Model,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<string>? Tools = null);
