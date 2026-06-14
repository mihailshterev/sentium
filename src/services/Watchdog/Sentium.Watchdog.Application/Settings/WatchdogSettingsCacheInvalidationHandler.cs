using Microsoft.Extensions.Caching.Hybrid;
using Sentium.Infrastructure.Settings;
using Sentium.Shared.Constants;

namespace Sentium.Watchdog.Application.Settings;

public sealed class WatchdogSettingsCacheInvalidationHandler(HybridCache cache) : ISettingsCacheInvalidationHandler
{
    public async ValueTask<bool> TryInvalidateAsync(string settingsKey, Guid? userId, CancellationToken ct)
    {
        if (settingsKey != SettingsKeys.Watchdog)
        {
            return false;
        }

        await cache.RemoveAsync(WatchdogSettingsCacheKeys.Runtime(userId), ct);
        return true;
    }
}
