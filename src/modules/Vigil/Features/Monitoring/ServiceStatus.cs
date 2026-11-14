using System.Text.Json.Serialization;

namespace Sentium.Vigil.Features.Monitoring;

[JsonConverter(typeof(JsonStringEnumConverter<ServiceStatus>))]
public enum ServiceStatus
{
    Unknown,
    Healthy,
    Degraded,
    Unhealthy
}
