using Microsoft.Extensions.Caching.Memory;

namespace Sentium.Praxis.Features.Models;

/// <summary>
/// Resolves how many tokens one request to a chat model may use - llama-server's per-slot context
/// (<c>ctx-size / parallel</c> in <c>infra/llm/models.ini</c>). Agents size their context compaction from it.
/// The window is server configuration, so it is read from the server, never sent with a request.
/// </summary>
public interface IModelContextWindowProvider
{
    ValueTask<int> GetContextWindowAsync(string modelId, CancellationToken ct = default);
}

/// <summary>
/// Reads the window from the <see cref="IModelCatalog"/> (<c>meta.n_ctx</c>), cached briefly so building an agent
/// rarely costs a round-trip. The server reports it only for a loaded model; until then, or while the server is
/// unreachable, <see cref="DefaultContextWindow"/> is assumed.
/// </summary>
public sealed class CatalogModelContextWindowProvider(
    IModelCatalog catalog,
    IMemoryCache cache,
    ILogger<CatalogModelContextWindowProvider> logger) : IModelContextWindowProvider
{
    /// <summary>
    /// The per-slot context of the shipped presets (32k context shared by 2 slots). Underestimating only starts
    /// compaction early; overestimating would let a long tool loop overflow the slot.
    /// </summary>
    public const int DefaultContextWindow = 16384;

    private static readonly TimeSpan ReportedTtl = TimeSpan.FromMinutes(10);

    // Short, so a model that is still loading reports its real window soon after.
    private static readonly TimeSpan FallbackTtl = TimeSpan.FromSeconds(30);

    public async ValueTask<int> GetContextWindowAsync(string modelId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);

        if (cache.TryGetValue(CacheKey(modelId), out int cached))
        {
            return cached;
        }

        int? resolved = null;

        // One listing reports every loaded model; cache them all.
        foreach (var model in await catalog.ListAsync(ct) ?? [])
        {
            if (model.ContextLength is > 0 and var window)
            {
                cache.Set(CacheKey(model.Id), window, ReportedTtl);

                if (string.Equals(model.Id, modelId, StringComparison.Ordinal))
                {
                    resolved = window;
                }
            }
        }

        if (resolved is { } reported)
        {
            return reported;
        }

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("No context window reported for model {Model}; assuming {Window} tokens", modelId, DefaultContextWindow);
        }

        cache.Set(CacheKey(modelId), DefaultContextWindow, FallbackTtl);
        return DefaultContextWindow;
    }

    private static string CacheKey(string modelId) => $"praxis:model-context-window:{modelId}";
}
