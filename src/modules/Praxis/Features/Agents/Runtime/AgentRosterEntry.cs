namespace Sentium.Praxis.Features.Agents.Runtime;

public sealed record AgentRosterEntry(string Name, string Description, AgentKind Kind, string? Model);
