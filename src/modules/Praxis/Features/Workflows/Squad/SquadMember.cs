namespace Sentium.Praxis.Features.Workflows.Squad;

/// <summary>
/// An agent available to a squad run.
/// </summary>
/// <param name="Name">Agent name (native role or the user's database agent).</param>
/// <param name="Description">One-line summary shown to the planner.</param>
/// <param name="Instructions">The agent's role instructions.</param>
/// <param name="Model">Model override, or <see langword="null"/> for the configured default.</param>
public sealed record SquadMember(string Name, string Description, string Instructions, string? Model = null);
