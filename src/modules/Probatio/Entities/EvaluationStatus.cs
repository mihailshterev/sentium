using System.Text.Json.Serialization;

namespace Sentium.Probatio.Entities;

[JsonConverter(typeof(JsonStringEnumConverter<EvaluationStatus>))]
internal enum EvaluationStatus
{
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled,
}
