using Microsoft.Extensions.Caching.Hybrid;
using Sentium.Infrastructure.Security;

namespace Sentium.Infrastructure.Caching;

public sealed class ScopedCache(HybridCache cache, ICurrentUser currentUser) : IScopedCache
{
    private string Scope => currentUser.IsSovereign ? "sovereign" : currentUser.UserId?.ToString() ?? "anon";

    public ValueTask<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, ValueTask<T>> factory, string tag, CancellationToken ct = default)
        => cache.GetOrCreateAsync($"{key}:{Scope}", factory, tags: [$"{tag}:{Scope}"], cancellationToken: ct);

    public ValueTask InvalidateTagAsync(string tag, CancellationToken ct = default)
        => cache.RemoveByTagAsync($"{tag}:{Scope}", ct);

    public ValueTask RemoveAsync(string key, CancellationToken ct = default)
        => cache.RemoveAsync($"{key}:{Scope}", ct);
}
