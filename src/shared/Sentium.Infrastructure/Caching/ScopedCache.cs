using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Sentium.Infrastructure.Security;

namespace Sentium.Infrastructure.Caching;

public sealed class ScopedCache(HybridCache cache, ICurrentUser currentUser, ILogger<ScopedCache> logger) : IScopedCache
{
    private string Scope => currentUser.IsSovereign ? "sovereign" : currentUser.UserId?.ToString() ?? "anon";

    public async ValueTask<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, ValueTask<T>> factory, string tag, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(factory);

        var scopedKey = $"{key}:{Scope}";

        try
        {
            return await cache.GetOrCreateAsync(scopedKey, factory, tags: [$"{tag}:{Scope}"], cancellationToken: ct);
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
            await cache.RemoveByTagAsync($"{tag}:{Scope}", ct);
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
            await cache.RemoveAsync($"{key}:{Scope}", ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Cache removal failed for key {CacheKey}.", key);
        }
    }
}
