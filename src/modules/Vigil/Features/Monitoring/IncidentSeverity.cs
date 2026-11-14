using System.Text.Json.Serialization;

namespace Sentium.Vigil.Features.Monitoring;

[JsonConverter(typeof(JsonStringEnumConverter<IncidentSeverity>))]
internal enum IncidentSeverity
{
    Warning,
    Critical
}
