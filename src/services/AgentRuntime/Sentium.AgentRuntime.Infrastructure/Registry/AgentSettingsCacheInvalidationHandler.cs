using Microsoft.Extensions.Caching.Hybrid;
using Sentium.Infrastructure.Settings;
using Sentium.Shared.Constants;

namespace Sentium.AgentRuntime.Infrastructure.Registry;

public sealed class AgentSettingsCacheInvalidationHandler(HybridCache cache) : ISettingsCacheInvalidationHandler
{
    public async ValueTask<bool> TryInvalidateAsync(string settingsKey, Guid? userId, CancellationToken ct)
    {
        switch (settingsKey)
        {
            case SettingsKeys.Harness:
            {
                await cache.RemoveAsync(AgentRuntimeSettingsCacheKeys.Snapshot(userId), ct);
                return true;
            }
            case SettingsKeys.Ollama:
            {
                await cache.RemoveByTagAsync(AgentRuntimeSettingsCacheKeys.SnapshotTag, ct);
                return true;
            }
            default:
            {
                return false;
            }
        }
    }
}
