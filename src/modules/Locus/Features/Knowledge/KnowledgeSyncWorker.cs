using Microsoft.Extensions.Logging;
using Sentium.Infrastructure.Security;

namespace Sentium.Locus.Features.Knowledge;

/// <summary>
/// Drains the knowledge-sync outbox in the background so asset saves never wait for embeddings. It
/// reconciles once at startup, then processes due entries whenever <see cref="KnowledgeSyncSignal"/> rings
/// or a retry becomes due (checking at least every minute).
/// </summary>
public sealed class KnowledgeSyncWorker(
    IServiceScopeFactory scopeFactory,
    KnowledgeSyncSignal signal,
    TimeProvider timeProvider,
    ILogger<KnowledgeSyncWorker> logger) : BackgroundService
{
    private static readonly TimeSpan IdlePoll = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan MinWait = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        try
        {
            var queued = await InScopeAsync((p, ct) => p.ReconcileAsync(ct), stoppingToken);
            if (queued > 0 && logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Knowledge sync reconciliation queued {Count} asset(s)", queued);
            }
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Knowledge sync reconciliation failed");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var wait = IdlePoll;

            try
            {
                var due = await InScopeAsync((p, ct) => p.GetDueAsync(KnowledgeSyncProcessor.BatchSize, ct), stoppingToken);

                foreach (var assetId in due)
                {
                    await InScopeAsync((p, ct) => p.ProcessAsync(assetId, ct), stoppingToken);
                }

                if (due.Count == KnowledgeSyncProcessor.BatchSize)
                {
                    continue;
                }

                var nextDueAt = await InScopeAsync((p, ct) => p.GetNextDueAtAsync(ct), stoppingToken);
                if (nextDueAt is { } next)
                {
                    var untilNext = next - timeProvider.GetUtcNow();
                    wait = untilNext < MinWait ? MinWait : untilNext > IdlePoll ? IdlePoll : untilNext;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Knowledge sync loop failed; retrying shortly");
            }

            await signal.WaitAsync(wait, timeProvider, stoppingToken);
        }
    }

    private async Task<T> InScopeAsync<T>(Func<KnowledgeSyncProcessor, CancellationToken, Task<T>> action, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<SystemScopeContext>().Activate();

        var processor = scope.ServiceProvider.GetRequiredService<KnowledgeSyncProcessor>();
        return await action(processor, ct);
    }
}
