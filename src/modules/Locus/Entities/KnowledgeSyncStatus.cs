using System.Text.Json.Serialization;

namespace Sentium.Locus.Entities;

[JsonConverter(typeof(JsonStringEnumConverter<KnowledgeSyncStatus>))]
public enum KnowledgeSyncStatus
{
    Pending,
    Failed
}
