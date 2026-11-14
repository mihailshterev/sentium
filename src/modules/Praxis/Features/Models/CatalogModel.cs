namespace Sentium.Praxis.Features.Models;

/// <summary>
/// A declared chat model and its runtime status as reported by the inference server
/// (<c>loaded</c>, <c>loading</c>, <c>unloaded</c>, <c>sleeping</c>, ...).
/// </summary>
/// <param name="ContextLength">Context window per parallel request, in tokens, once the server has reported it.</param>
public sealed record CatalogModel(string Id, string Status, int? ContextLength = null);
