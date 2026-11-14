using System.Text.Json.Serialization;

namespace Sentium.Locus.Entities;

[JsonConverter(typeof(JsonStringEnumConverter<AttachmentKind>))]
internal enum AttachmentKind
{
    Photo,
    Manual,
    Receipt,
    Other
}
