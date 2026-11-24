using Sentium.Infrastructure.Security;

namespace Sentium.Praxis.Features.Learnings;

/// <summary>
/// Retries the embedding of learnings captured while the embedding server was unavailable; until then they are
/// stored but never recalled.
/// </summary>
internal sealed class LearningIngestionRetryWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<LearningIngestionRetryWorker> logger) : BackgroundService
{
    private const int BatchSize = 25;

    private static readonly TimeSpan RetryInterval = TimeSpan.FromMinutes(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(RetryInterval, timeProvider);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RetryAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task RetryAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<SystemScopeContext>().Activate();

            var ingested = await scope.ServiceProvider.GetRequiredService<IAgentLearningService>().RetryPendingIngestionAsync(BatchSize, ct);
            if (ingested > 0)
            {
                logger.LogInformation("Embedded {Count} learnings whose embedding had failed.", ingested);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Retrying the embedding of pending learnings failed; trying again later.");
        }
    }
}
