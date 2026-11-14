using System.Text.Json.Serialization;

namespace Sentium.Praxis.Common.Streaming;

internal sealed record StreamErrorFrame(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("type")] string Type = "error",
    [property: JsonPropertyName("done")] bool Done = true);
