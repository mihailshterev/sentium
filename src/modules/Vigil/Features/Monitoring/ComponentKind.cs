using System.Text.Json.Serialization;

namespace Sentium.Vigil.Features.Monitoring;

[JsonConverter(typeof(JsonStringEnumConverter<ComponentKind>))]
public enum ComponentKind
{
    Service,
    Infrastructure
}
