namespace Sentium.Praxis.Features.Models;

/// <summary>
/// A chat model the inference server serves, its runtime status (<c>loaded</c>, <c>loading</c>,
/// <c>unloaded</c>, <c>sleeping</c>, ...) and its context window per request in tokens, when known.
/// </summary>
public sealed record ModelInfo(string Id, string Status, int? ContextLength);
