using System.Text.Json.Serialization;

namespace Sentium.Praxis.Common.Streaming;

public sealed record StreamDoneFrame(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("done")] bool Done = true);
