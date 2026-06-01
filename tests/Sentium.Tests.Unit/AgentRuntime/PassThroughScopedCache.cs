using Sentium.Infrastructure.Caching;

namespace Sentium.Tests.Unit;

/// <summary>
/// Test double for <see cref="IScopedCache"/> that bypasses caching entirely, invoking the factory
/// on every read so tests exercise the underlying repository/service logic directly.
/// </summary>
internal sealed class PassThroughScopedCache : IScopedCache
{
    public ValueTask<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, ValueTask<T>> factory, string tag, CancellationToken ct = default)
        => factory(ct);

    public ValueTask InvalidateTagAsync(string tag, CancellationToken ct = default) => default;

    public ValueTask RemoveAsync(string key, CancellationToken ct = default) => default;
}
