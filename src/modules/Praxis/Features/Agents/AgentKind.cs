using System.Text.Json.Serialization;

namespace Sentium.Praxis.Features.Agents;

[JsonConverter(typeof(JsonStringEnumConverter<AgentKind>))]
internal enum AgentKind
{
    BuiltIn,
    Custom
}
