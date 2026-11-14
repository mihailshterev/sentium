using System.Text.Json.Serialization;

namespace Sentium.Tempus.Contracts.Models;

[JsonConverter(typeof(JsonStringEnumConverter<TimelineProposalKind>))]
public enum TimelineProposalKind
{
    /// <summary>
    /// A new task the agent suggests adding.
    /// </summary>
    CreateTask,

    /// <summary>
    /// A new day or month for an existing Task or Calendar Event.
    /// </summary>
    Reschedule,

    /// <summary>
    /// A new Calendar Event the agent suggests adding.
    /// </summary>
    CreateEvent
}
