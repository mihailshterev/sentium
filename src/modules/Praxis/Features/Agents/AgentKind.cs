using System.Text.Json.Serialization;

namespace Sentium.Praxis.Features.Agents;

[JsonConverter(typeof(JsonStringEnumConverter<AgentKind>))]
public enum AgentKind
{
    BuiltIn,
    Custom
}
