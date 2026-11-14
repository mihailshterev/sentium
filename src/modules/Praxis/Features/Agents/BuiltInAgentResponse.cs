namespace Sentium.Praxis.Features.Agents;

/// <summary>
/// A built-in agent with its tool assignment for the signed-in user.
/// </summary>
/// <param name="DefaultTools">The code-defined tool list; <see langword="null"/> = every tool.</param>
/// <param name="Tools">The tools the agent actually gets (the user's override, else the default); <see langword="null"/> = every tool.</param>
/// <param name="IsCustomized">Whether the user overrides the default.</param>
internal sealed record BuiltInAgentResponse(
    string Name,
    string Description,
    IReadOnlyList<string>? DefaultTools,
    IReadOnlyList<string>? Tools,
    bool IsCustomized);
