namespace Sentium.Infrastructure.Caching;

public interface IScopedCache
{
    ValueTask<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, ValueTask<T>> factory, string tag, CancellationToken ct = default);
    ValueTask InvalidateTagAsync(string tag, CancellationToken ct = default);
    ValueTask RemoveAsync(string key, CancellationToken ct = default);
}
