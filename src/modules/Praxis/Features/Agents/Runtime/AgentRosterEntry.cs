namespace Sentium.Praxis.Features.Agents.Runtime;

internal sealed record AgentRosterEntry(string Name, string Description, AgentKind Kind, string? Model);
