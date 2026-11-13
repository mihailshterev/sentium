using System.Text.Json.Serialization;

namespace Sentium.Praxis.Features.Agents;

[JsonConverter(typeof(JsonStringEnumConverter<AgentKind>))]
public enum AgentKind
{
    BuiltIn,
    Custom
}

public sealed record AgentRosterEntry(string Name, string Description, AgentKind Kind, string? Model);
