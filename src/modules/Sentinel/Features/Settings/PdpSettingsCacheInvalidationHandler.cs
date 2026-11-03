using Microsoft.Extensions.Caching.Hybrid;
using Sentium.Infrastructure.Settings;
using Sentium.Core.Constants;

namespace Sentium.Sentinel.Features.Settings;

public sealed class PdpSettingsCacheInvalidationHandler(HybridCache cache) : ISettingsCacheInvalidationHandler
{
    public async ValueTask<bool> TryInvalidateAsync(string settingsKey, Guid? userId, CancellationToken ct)
    {
        if (settingsKey != SettingsKeys.Pdp)
        {
            return false;
        }

        await cache.RemoveAsync(SentinelSettingsCacheKeys.PdpRuntime(userId), ct);
        return true;
    }
}
