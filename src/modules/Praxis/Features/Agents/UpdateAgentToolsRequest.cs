namespace Sentium.Praxis.Features.Agents;

/// <summary>
/// Replaces a built-in agent's tools for the signed-in user. <paramref name="Tools"/> <see langword="null"/> = every tool.
/// </summary>
public sealed record UpdateAgentToolsRequest(IReadOnlyList<string>? Tools);
