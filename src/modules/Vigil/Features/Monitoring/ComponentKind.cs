using System.Text.Json.Serialization;

namespace Sentium.Vigil.Features.Monitoring;

[JsonConverter(typeof(JsonStringEnumConverter<ComponentKind>))]
internal enum ComponentKind
{
    Service,
    Infrastructure
}
