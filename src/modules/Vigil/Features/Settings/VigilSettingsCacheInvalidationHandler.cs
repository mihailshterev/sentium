using Microsoft.Extensions.Caching.Hybrid;
using Sentium.Infrastructure.Settings;
using Sentium.Core.Constants;

namespace Sentium.Vigil.Features.Settings;

public sealed class VigilSettingsCacheInvalidationHandler(HybridCache cache) : ISettingsCacheInvalidationHandler
{
    public async ValueTask<bool> TryInvalidateAsync(string settingsKey, Guid? userId, CancellationToken ct)
    {
        if (settingsKey != SettingsKeys.Vigil)
        {
            return false;
        }

        await cache.RemoveAsync(VigilSettingsCacheKeys.Runtime(userId), ct);
        return true;
    }
}
