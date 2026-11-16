using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Sentium.Infrastructure.Security;

namespace Sentium.Infrastructure.Caching;

/// <summary>
/// Values are stored per caller scope (a user, all Sovereigns, or background work), because the query filters make
/// the same query return different rows for each. Tags are not scoped: a write by any caller (a Sovereign editing a
/// member's data, a background worker updating a status) must invalidate every scope's copy, or the owner keeps
/// seeing stale data until it expires.
/// </summary>
internal sealed class ScopedCache(HybridCache cache, ICurrentUser currentUser, ILogger<ScopedCache> logger) : IScopedCache
{
    private const string KeyTagPrefix = "key:";

    private string Scope => currentUser.IsSovereign ? "sovereign" : currentUser.UserId?.ToString() ?? "anon";

    public async ValueTask<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, ValueTask<T>> factory, string tag, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(factory);

        var scopedKey = $"{key}:{Scope}";

        try
        {
            return await cache.GetOrCreateAsync(scopedKey, factory, tags: [tag, KeyTag(key)], cancellationToken: ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Cache read failed for key {CacheKey}; falling back to source.", scopedKey);
            return await factory(ct);
        }
    }

    public async ValueTask InvalidateTagAsync(string tag, CancellationToken ct = default)
    {
        try
        {
            await cache.RemoveByTagAsync(tag, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Cache invalidation failed for tag {CacheTag}.", tag);
        }
    }

    public async ValueTask RemoveAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await cache.RemoveByTagAsync(KeyTag(key), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Cache removal failed for key {CacheKey}.", key);
        }
    }

    private static string KeyTag(string key) => KeyTagPrefix + key;
}
