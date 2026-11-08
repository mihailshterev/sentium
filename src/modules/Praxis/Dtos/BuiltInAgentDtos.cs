namespace Sentium.Praxis.Dtos;

/// <summary>
/// A built-in agent with its tool assignment for the signed-in user.
/// </summary>
/// <param name="DefaultTools">The code-defined tool list; <see langword="null"/> = every tool.</param>
/// <param name="Tools">The tools the agent actually gets (the user's override, else the default); <see langword="null"/> = every tool.</param>
/// <param name="IsCustomized">Whether the user overrides the default.</param>
public sealed record BuiltInAgentResponse(
    string Name,
    string Description,
    IReadOnlyList<string>? DefaultTools,
    IReadOnlyList<string>? Tools,
    bool IsCustomized);

/// <summary>
/// Replaces a built-in agent's tools for the signed-in user. <paramref name="Tools"/> <see langword="null"/> = every tool.
/// </summary>
public sealed record UpdateAgentToolsRequest(IReadOnlyList<string>? Tools);
