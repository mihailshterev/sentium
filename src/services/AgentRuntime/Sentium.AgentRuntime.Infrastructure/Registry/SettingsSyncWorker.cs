using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sentium.Infrastructure.Messaging;
using Sentium.Shared.Constants;
using Sentium.Shared.Events;

namespace Sentium.AgentRuntime.Infrastructure.Registry;

/// <summary>
/// Subscribes to the Registry's cache-invalidation event and evicts the local L1 entry
/// so this service instance always sees fresh settings on the next read.
/// L2 (Redis) is already evicted by Registry itself; this worker only clears L1.
/// </summary>
public sealed class SettingsSyncWorker(
    IEventBus eventBus,
    HybridCache hybridCache,
    ILogger<SettingsSyncWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("SettingsSyncWorker started — listening on {Subject}", NatsSubjects.SettingsInvalidated);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await foreach (var msg in eventBus.SubscribeStreamAsync<SettingsInvalidatedEvent>(NatsSubjects.SettingsInvalidated, ct: stoppingToken))
                {
                    if (msg.Data is null)
                    {
                        continue;
                    }

                    try
                    {
                        await hybridCache.RemoveAsync(msg.Data.CacheKey, stoppingToken);

                        logger.LogInformation(
                            "L1 cache evicted for key '{Key}' (settings updated at {At})",
                            msg.Data.CacheKey,
                            msg.Data.InvalidatedAt);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        logger.LogError(ex, "Failed to evict cache key '{Key}'", msg.Data.CacheKey);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SettingsSyncWorker subscription failed; restarting in 5 s");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
