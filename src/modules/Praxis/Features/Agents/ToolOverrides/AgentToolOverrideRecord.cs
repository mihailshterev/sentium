namespace Sentium.Praxis.Features.Agents.ToolOverrides;

/// <summary>
/// A user's tool list for a built-in agent. <paramref name="Tools"/> <see langword="null"/> = every tool.
/// </summary>
internal sealed record AgentToolOverrideRecord(string AgentName, IReadOnlyList<string>? Tools);
