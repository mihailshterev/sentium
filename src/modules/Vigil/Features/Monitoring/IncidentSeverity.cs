using System.Text.Json.Serialization;

namespace Sentium.Vigil.Features.Monitoring;

[JsonConverter(typeof(JsonStringEnumConverter<IncidentSeverity>))]
public enum IncidentSeverity
{
    Warning,
    Critical
}
