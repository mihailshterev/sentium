using System.Text.Json.Serialization;

namespace Sentium.Tempus.Contracts.Models;

[JsonConverter(typeof(JsonStringEnumConverter<TimelineProposalStatus>))]
public enum TimelineProposalStatus
{
    Pending,
    Accepted,
    Rejected,

    /// <summary>
    /// Overtaken before the user answered: the task moved, was completed, or a newer proposal replaced it.
    /// </summary>
    Superseded
}
