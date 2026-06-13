using System.Text.Json.Serialization;

namespace Sentium.AgentRuntime.Core.Dtos;

public sealed record PullModelRequest(string Name);

public sealed record DeleteModelResult(string DeletedModel, string DefaultModel, int AgentsReset);

public sealed record OllamaTagsResponse([property: JsonPropertyName("models")] List<OllamaModelInfo> Models);

public sealed record OllamaModelInfo(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("size")] long Size,
    [property: JsonPropertyName("digest")] string Digest,
    [property: JsonPropertyName("modified_at")] string ModifiedAt,
    [property: JsonPropertyName("details")] OllamaModelDetails? Details);

public sealed record OllamaModelDetails(
    [property: JsonPropertyName("format")] string Format,
    [property: JsonPropertyName("family")] string Family,
    [property: JsonPropertyName("parameter_size")] string ParameterSize,
    [property: JsonPropertyName("quantization_level")] string QuantizationLevel);

public sealed record OllamaPullRequest(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("stream")] bool Stream);

public sealed record OllamaDeleteRequest([property: JsonPropertyName("name")] string Name);
