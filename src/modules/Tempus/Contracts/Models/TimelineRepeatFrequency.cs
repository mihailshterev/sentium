using System.Text.Json.Serialization;

namespace Sentium.Tempus.Contracts.Models;

[JsonConverter(typeof(JsonStringEnumConverter<TimelineRepeatFrequency>))]
public enum TimelineRepeatFrequency
{
    Daily,
    Weekly,
    Monthly,
    Yearly
}
