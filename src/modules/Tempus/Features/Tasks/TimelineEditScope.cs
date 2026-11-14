using System.Text.Json.Serialization;

namespace Sentium.Tempus.Features.Tasks;

/// <summary>
/// Which occurrences of a Series an edit or delete applies to. Ignored for a one-off Entry.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<TimelineEditScope>))]
internal enum TimelineEditScope
{
    /// <summary>
    /// Only this occurrence, which is then detached from later Series edits.
    /// </summary>
    One,

    /// <summary>
    /// This occurrence and every later one: the Series ends the day before and, for an edit, a new one starts.
    /// </summary>
    Following
}
