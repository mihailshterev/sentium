using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sentium.Infrastructure.Messaging;
using Sentium.Shared.Constants;
using Sentium.Shared.Events;

namespace Sentium.Infrastructure.Settings;

/// <summary>
/// Subscribes to the Registry's semantic settings-invalidation event and lets this service's
/// registered <see cref="ISettingsCacheInvalidationHandler"/>s evict their own cache entries, so the
/// instance always sees fresh settings on the next read.
/// <para/>
/// The worker is intentionally generic: it knows nothing about cache keys. Each service translates
/// the <c>(Key, UserId)</c> identity to its own private key(s) via its handler(s), which keeps cache
/// keys out of the cross-service contract (different services may cache different shapes).
/// </summary>
public sealed class SettingsSyncWorker(
    IEventBus eventBus,
    IEnumerable<ISettingsCacheInvalidationHandler> handlers,
    ILogger<SettingsSyncWorker> logger) : BackgroundService
{
    private readonly ISettingsCacheInvalidationHandler[] _handlers = [.. handlers];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("SettingsSyncWorker started - listening on {Subject}", NatsSubjects.SettingsInvalidated);

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

                    await InvalidateAsync(msg.Data, stoppingToken);
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

    private async Task InvalidateAsync(SettingsInvalidatedEvent evt, CancellationToken ct)
    {
        var handled = false;

        foreach (var handler in _handlers)
        {
            try
            {
                handled |= await handler.TryInvalidateAsync(evt.Key, evt.UserId, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Handler {Handler} failed to evict settings '{Key}' (scope {Scope})",
                    handler.GetType().Name, evt.Key, evt.UserId?.ToString() ?? "global");
            }
        }

        if (handled)
        {
            logger.LogInformation(
                "Cache evicted for settings '{Key}' (scope {Scope}, updated at {At})",
                evt.Key,
                evt.UserId?.ToString() ?? "global",
                evt.InvalidatedAt);
        }
    }
}
