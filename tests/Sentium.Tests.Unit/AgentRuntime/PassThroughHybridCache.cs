using Microsoft.Extensions.Caching.Hybrid;

namespace Sentium.Tests.Unit;

internal sealed class PassThroughHybridCache : HybridCache
{
    public override ValueTask<T> GetOrCreateAsync<TState, T>(
        string key,
        TState state,
        Func<TState, CancellationToken, ValueTask<T>> factory,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
        => factory(state, cancellationToken);

    public override ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
        => default;

    public override ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default)
        => default;

    public override ValueTask SetAsync<T>(
        string key,
        T value,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
        => default;
}
