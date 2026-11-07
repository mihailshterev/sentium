namespace Sentium.Praxis.Features.Models;

/// <summary>
/// The chat models the inference server can serve. The set is deployment configuration
/// (<c>infra/llm/models.ini</c>); the catalog only reports it and loads or unloads models in memory.
/// </summary>
public interface IModelCatalog
{
    /// <summary>
    /// Lists the declared chat models with their runtime status.
    /// </summary>
    /// <returns>The models ordered by id, or <see langword="null"/> when the inference server is unreachable or fails.</returns>
    Task<IReadOnlyList<CatalogModel>?> ListAsync(CancellationToken ct = default);

    /// <summary>
    /// Starts loading a declared model into memory. Returns once the server has accepted the request;
    /// <see cref="ListAsync"/> reports <c>loading</c> until it is ready.
    /// </summary>
    Task<ModelActionResult> LoadAsync(string modelId, CancellationToken ct = default);

    /// <summary>
    /// Starts unloading a model from memory to free GPU/CPU memory; it stays declared and can be loaded again.
    /// Returns once the server has accepted the request.
    /// </summary>
    Task<ModelActionResult> UnloadAsync(string modelId, CancellationToken ct = default);
}

/// <summary>
/// A declared chat model and its runtime status as reported by the inference server
/// (<c>loaded</c>, <c>loading</c>, <c>unloaded</c>, <c>sleeping</c>, ...).
/// </summary>
/// <param name="ContextLength">Context window per parallel request, in tokens, once the server has reported it.</param>
public sealed record CatalogModel(string Id, string Status, int? ContextLength = null);

public enum ModelActionResult
{
    /// <summary>The server accepted the request.</summary>
    Accepted,

    /// <summary>The model is not declared on the server.</summary>
    NotFound,

    /// <summary>The model is in a state that does not allow the action (e.g. load while it is still running).</summary>
    Conflict,

    /// <summary>The server is unreachable or failed.</summary>
    Failed
}
