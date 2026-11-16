namespace Sentium.Infrastructure.Caching;

/// <summary>
/// Caches values per caller scope (the current user, Sovereigns, or background work). Invalidation is not scoped:
/// <see cref="InvalidateTagAsync"/> and <see cref="RemoveAsync"/> drop the entry for every scope.
/// </summary>
public interface IScopedCache
{
    ValueTask<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, ValueTask<T>> factory, string tag, CancellationToken ct = default);

    ValueTask InvalidateTagAsync(string tag, CancellationToken ct = default);

    ValueTask RemoveAsync(string key, CancellationToken ct = default);
}
