using System.Text.Json.Serialization;

namespace Sentium.Probatio.Entities;

[JsonConverter(typeof(JsonStringEnumConverter<EvalTurnRole>))]
internal enum EvalTurnRole
{
    User,
    Assistant,
}
