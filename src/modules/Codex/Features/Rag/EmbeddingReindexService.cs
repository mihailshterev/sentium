namespace Sentium.Codex.Features.Rag;

/// <summary>
/// After startup, re-embeds every stored chunk produced by another embedding model (see
/// <see cref="EmbeddingReindexer"/>), then stops. Retries with backoff while the embedding server is
/// still loading or unreachable. Search quality is degraded only while it runs.
/// </summary>
internal sealed class EmbeddingReindexService(
    IServiceScopeFactory scopeFactory,
    IHostApplicationLifetime lifetime,
    ILogger<EmbeddingReindexService> logger) : BackgroundService
{
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!await WaitForStartedAsync(stoppingToken))
        {
            return;
        }

        var reindexed = 0;
        var retryDelay = InitialRetryDelay;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var reindexer = scope.ServiceProvider.GetRequiredService<EmbeddingReindexer>();

                if (reindexed == 0)
                {
                    var pending = await reindexer.CountPendingAsync(stoppingToken);
                    if (pending == 0)
                    {
                        return;
                    }

                    logger.LogInformation("Re-embedding {Pending} knowledge chunks with the configured embedding model", pending);
                }

                var updated = await reindexer.ReindexBatchAsync(stoppingToken);
                if (updated == 0)
                {
                    logger.LogInformation("Re-embedded {Count} knowledge chunks", reindexed);
                    return;
                }

                reindexed += updated;
                retryDelay = InitialRetryDelay;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Re-embedding knowledge chunks failed; retrying in {Delay}", retryDelay);

                try
                {
                    await Task.Delay(retryDelay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                retryDelay = TimeSpan.FromTicks(Math.Min(retryDelay.Ticks * 2, MaxRetryDelay.Ticks));
            }
        }
    }

    private async Task<bool> WaitForStartedAsync(CancellationToken stoppingToken)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var onStarted = lifetime.ApplicationStarted.Register(() => started.TrySetResult());
        using var onStopping = stoppingToken.Register(() => started.TrySetCanceled(stoppingToken));

        try
        {
            await started.Task;
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
