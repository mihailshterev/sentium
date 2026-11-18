using Sentium.Infrastructure.Security;

namespace Sentium.Memoria.Features.Indexing;

/// <summary>
/// Drains the note index outbox in the background so syncs never wait for embeddings. It reconciles once at startup,
/// then processes due entries whenever <see cref="NoteIndexSignal"/> rings or a retry becomes due (checking at least
/// every minute).
/// </summary>
internal sealed class NoteIndexWorker(
    IServiceScopeFactory scopeFactory,
    NoteIndexSignal signal,
    TimeProvider timeProvider,
    ILogger<NoteIndexWorker> logger) : BackgroundService
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
                var due = await InScopeAsync((p, ct) => p.GetDueAsync(NoteIndexProcessor.BatchSize, ct), stoppingToken);

                foreach (var noteId in due)
                {
                    await InScopeAsync((p, ct) => p.ProcessAsync(noteId, ct), stoppingToken);
                }

                if (due.Count == NoteIndexProcessor.BatchSize)
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
                logger.LogError(ex, "Vault note index loop failed; retrying shortly");
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
                logger.LogInformation("Vault note index reconciliation queued {Count} note(s)", queued);
            }
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Vault note index reconciliation failed");
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

    private async Task<T> InScopeAsync<T>(Func<NoteIndexProcessor, CancellationToken, Task<T>> action, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<SystemScopeContext>().Activate();

        var processor = scope.ServiceProvider.GetRequiredService<NoteIndexProcessor>();
        return await action(processor, ct);
    }
}
