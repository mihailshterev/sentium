using System.Text.Json.Serialization;

namespace Sentium.Tempus.Contracts.Models;

[JsonConverter(typeof(JsonStringEnumConverter<TimelineTaskStatus>))]
public enum TimelineTaskStatus
{
    Todo,
    InProgress,
    Done
}
