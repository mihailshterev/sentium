namespace Sentium.Infrastructure.Caching;

/// <summary>
/// Tag-aware cache that resolves values via a factory and supports tag-based invalidation.
/// </summary>
public interface IScopedCache
{
    ValueTask<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, ValueTask<T>> factory, string tag, CancellationToken ct = default);
    ValueTask InvalidateTagAsync(string tag, CancellationToken ct = default);
    ValueTask RemoveAsync(string key, CancellationToken ct = default);
}
