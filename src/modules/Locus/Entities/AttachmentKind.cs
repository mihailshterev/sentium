using System.Text.Json.Serialization;

namespace Sentium.Locus.Entities;

[JsonConverter(typeof(JsonStringEnumConverter<AttachmentKind>))]
public enum AttachmentKind
{
    Photo,
    Manual,
    Receipt,
    Other
}
