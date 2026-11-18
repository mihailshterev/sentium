using System.Text.Json.Serialization;

namespace Sentium.Probatio.Entities;

[JsonConverter(typeof(JsonStringEnumConverter<CheckOutcomeStatus>))]
internal enum CheckOutcomeStatus
{
    Passed,
    Failed,

    /// <summary>
    /// Not graded; does not count towards the case passing.
    /// </summary>
    Skipped,
}
