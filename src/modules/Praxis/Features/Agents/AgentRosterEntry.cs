namespace Sentium.Praxis.Features.Agents;

public sealed record AgentRosterEntry(string Name, string Description, AgentKind Kind, string? Model);
