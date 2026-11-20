using System.Text.Json.Serialization;

namespace Sentium.Scheduling.Features.Jobs;

/// <summary>
/// What a Schedule runs. Stored by name in the job data; jobs written before kinds existed have none and are
/// <see cref="Script"/>.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ScheduleKind>))]
internal enum ScheduleKind
{
    Script,
    Agent,
    Workflow
}
