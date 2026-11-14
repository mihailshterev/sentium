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
    /// A new day or month for an existing task.
    /// </summary>
    Reschedule
}
