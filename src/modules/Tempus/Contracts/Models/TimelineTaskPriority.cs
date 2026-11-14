using System.Text.Json.Serialization;

namespace Sentium.Tempus.Contracts.Models;

[JsonConverter(typeof(JsonStringEnumConverter<TimelineTaskPriority>))]
public enum TimelineTaskPriority
{
    Low,
    Normal,
    High,
    Urgent
}
