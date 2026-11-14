namespace Sentium.Praxis.Features.Models;

/// <summary>
/// The chat models the inference server can serve. The set is deployment configuration
/// (<c>infra/llm/models.ini</c>); the catalog only reports it and loads or unloads models in memory.
/// </summary>
internal interface IModelCatalog
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
