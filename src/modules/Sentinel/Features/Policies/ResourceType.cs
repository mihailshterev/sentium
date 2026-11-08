using System.Text.Json.Serialization;

namespace Sentium.Sentinel.Features.Policies;

[JsonConverter(typeof(JsonStringEnumConverter<ResourceType>))]
public enum ResourceType
{
    File,
    VectorDb,
    Workflow,
    Memory,
    External,
    Code,
    Inventory,
    Timeline,
    Any
}
