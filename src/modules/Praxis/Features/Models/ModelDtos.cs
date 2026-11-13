namespace Sentium.Praxis.Features.Models;

/// <summary>
/// A chat model the inference server serves, its runtime status (<c>loaded</c>, <c>loading</c>,
/// <c>unloaded</c>, <c>sleeping</c>, ...) and its context window per request in tokens, when known.
/// </summary>
public sealed record ModelInfo(string Id, string Status, int? ContextLength);

/// <summary>
/// The declared chat models plus the effective default model (Registry setting, else configuration).
/// </summary>
public sealed record ModelsResponse(string DefaultModel, IReadOnlyList<ModelInfo> Models);

/// <summary>
/// Names the model to load into or unload from memory.
/// </summary>
public sealed record ModelActionRequest(string Model);
