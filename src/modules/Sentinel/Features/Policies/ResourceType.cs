using System.Text.Json.Serialization;

namespace Sentium.Sentinel.Features.Policies;

// Serialized by name: the Sentinel API has always exposed these as strings.
[JsonConverter(typeof(JsonStringEnumConverter<ResourceType>))]
public enum ResourceType
{
    File,
    VectorDb,
    Workflow,
    Memory,
    External,
    Code,
    Any
}
