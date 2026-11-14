using System.Text.Json.Serialization;

namespace Sentium.Tempus.Contracts.Models;

[JsonConverter(typeof(JsonStringEnumConverter<TimelineNoteKind>))]
public enum TimelineNoteKind
{
    Note,
    StatusUpdate,
    ProgressReport
}
