using System.Text.Json.Serialization;

namespace Sentium.Sentinel.Contracts;

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
    Vault,
    Any
}
