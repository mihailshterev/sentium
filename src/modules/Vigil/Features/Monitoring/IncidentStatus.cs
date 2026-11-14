using System.Text.Json.Serialization;

namespace Sentium.Vigil.Features.Monitoring;

[JsonConverter(typeof(JsonStringEnumConverter<IncidentStatus>))]
public enum IncidentStatus
{
    Open,
    Resolved
}
