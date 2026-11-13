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

[JsonConverter(typeof(JsonStringEnumConverter<TimelineTaskStatus>))]
public enum TimelineTaskStatus
{
    Todo,
    InProgress,
    Done
}

/// <summary>
/// Who wrote something on the timeline: the user, or an assistant acting for them.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<TimelineAuthorKind>))]
public enum TimelineAuthorKind
{
    User,
    Agent
}

[JsonConverter(typeof(JsonStringEnumConverter<TimelineNoteKind>))]
public enum TimelineNoteKind
{
    Note,
    StatusUpdate,
    ProgressReport
}

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
