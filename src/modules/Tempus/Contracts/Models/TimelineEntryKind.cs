using System.Text.Json.Serialization;

namespace Sentium.Tempus.Contracts.Models;

/// <summary>
/// What an Entry on the timeline is. Chosen when the Entry is created and never changed.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<TimelineEntryKind>))]
public enum TimelineEntryKind
{
    /// <summary>
    /// Something a person must tick as done; it can become overdue.
    /// </summary>
    Task,

    /// <summary>
    /// Something that happens at its date; it is never ticked and never overdue, only past.
    /// </summary>
    CalendarEvent
}
