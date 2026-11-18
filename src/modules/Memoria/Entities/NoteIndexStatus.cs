using System.Text.Json.Serialization;

namespace Sentium.Memoria.Entities;

[JsonConverter(typeof(JsonStringEnumConverter<NoteIndexStatus>))]
internal enum NoteIndexStatus
{
    Pending,
    Failed
}
