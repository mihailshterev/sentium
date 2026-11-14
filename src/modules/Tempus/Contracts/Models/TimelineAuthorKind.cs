using System.Text.Json.Serialization;

namespace Sentium.Tempus.Contracts.Models;

/// <summary>
/// Who wrote something on the timeline: the user, or an assistant acting for them.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<TimelineAuthorKind>))]
public enum TimelineAuthorKind
{
    User,
    Agent
}
