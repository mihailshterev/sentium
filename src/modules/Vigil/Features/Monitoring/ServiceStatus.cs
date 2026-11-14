using System.Text.Json.Serialization;

namespace Sentium.Vigil.Features.Monitoring;

[JsonConverter(typeof(JsonStringEnumConverter<ServiceStatus>))]
internal enum ServiceStatus
{
    Unknown,
    Healthy,
    Degraded,
    Unhealthy
}
