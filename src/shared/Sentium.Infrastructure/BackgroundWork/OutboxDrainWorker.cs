using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sentium.Infrastructure.Security;

namespace Sentium.Infrastructure.BackgroundWork;

/// <summary>
/// Drains an outbox in the background so the writes that fill it never wait for the slow work (embeddings). It
/// reconciles once at startup, then processes due entries whenever the outbox's <see cref="WakeSignal"/> rings or a
/// retry becomes due, checking at least every minute.
/// </summary>
public abstract class OutboxDrainWorker<TProcessor>(
    IServiceScopeFactory scopeFactory,
    WakeSignal signal,
    TimeProvider timeProvider,
    ILogger logger,
    string outboxName) : BackgroundService
    where TProcessor : class, IOutboxProcessor
{
    private static readonly TimeSpan IdlePoll = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan MinWait = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        await ReconcileAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var wait = IdlePoll;

            try
            {
                var due = await InScopeAsync((p, ct) => p.GetDueAsync(TProcessor.BatchSize, ct), stoppingToken);

                foreach (var id in due)
                {
                    await InScopeAsync((p, ct) => ProcessAsync(p, id, ct), stoppingToken);
                }

                if (due.Count == TProcessor.BatchSize)
                {
                    continue;
                }

                wait = await GetWaitAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "The {Outbox} loop failed; retrying shortly", outboxName);
            }

            await signal.WaitAsync(wait, timeProvider, stoppingToken);
        }
    }

    private async Task ReconcileAsync(CancellationToken stoppingToken)
    {
        try
        {
            var queued = await InScopeAsync((p, ct) => p.ReconcileAsync(ct), stoppingToken);
            if (queued > 0 && logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("{Outbox} reconciliation queued {Count} entries", outboxName, queued);
            }
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogError(ex, "{Outbox} reconciliation failed", outboxName);
        }
    }

    private async Task<TimeSpan> GetWaitAsync(CancellationToken stoppingToken)
    {
        var nextDueAt = await InScopeAsync((p, ct) => p.GetNextDueAtAsync(ct), stoppingToken);
        if (nextDueAt is not { } next)
        {
            return IdlePoll;
        }

        var untilNext = next - timeProvider.GetUtcNow();
        return untilNext < MinWait ? MinWait : untilNext > IdlePoll ? IdlePoll : untilNext;
    }

    private static async Task<bool> ProcessAsync(TProcessor processor, Guid id, CancellationToken ct)
    {
        await processor.ProcessEntryAsync(id, ct);
        return true;
    }

    private async Task<T> InScopeAsync<T>(Func<TProcessor, CancellationToken, Task<T>> action, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<SystemScopeContext>().Activate();

        var processor = scope.ServiceProvider.GetRequiredService<TProcessor>();
        return await action(processor, ct);
    }
}
