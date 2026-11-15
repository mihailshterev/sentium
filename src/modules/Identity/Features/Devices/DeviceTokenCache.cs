using Microsoft.Extensions.Caching.Memory;

namespace Sentium.Identity.Features.Devices;

/// <summary>
/// Short-lived in-process cache of token lookups, keyed by token hash. Only successful lookups are stored, so
/// guessing tokens cannot fill it.
/// </summary>
internal sealed class DeviceTokenCache(IMemoryCache cache)
{
    public AuthenticatedDevice? Get(string tokenHash) => cache.Get<AuthenticatedDevice>(Key(tokenHash));

    public void Set(string tokenHash, AuthenticatedDevice device) => cache.Set(Key(tokenHash), device, DeviceLimits.TokenCacheLifetime);

    public void Evict(string tokenHash) => cache.Remove(Key(tokenHash));

    private static string Key(string tokenHash) => $"identity:device-token:{tokenHash}";
}
