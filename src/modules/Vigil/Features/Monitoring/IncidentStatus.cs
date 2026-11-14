using System.Text.Json.Serialization;

namespace Sentium.Vigil.Features.Monitoring;

[JsonConverter(typeof(JsonStringEnumConverter<IncidentStatus>))]
internal enum IncidentStatus
{
    Open,
    Resolved
}
