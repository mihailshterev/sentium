using System.Text.Json.Serialization;

namespace Sentium.Locus.Entities;

[JsonConverter(typeof(JsonStringEnumConverter<KnowledgeSyncStatus>))]
internal enum KnowledgeSyncStatus
{
    Pending,
    Failed
}
