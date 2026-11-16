using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sentium.Infrastructure.Security;

namespace Sentium.Infrastructure.Retention;

/// <summary>
/// Runs every <see cref="IRetentionSweep"/> once at startup and then daily. A failing sweep is logged and retried at
/// the next interval without stopping the others.
/// </summary>
internal sealed class RetentionWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<RetentionWorker> logger) : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromDays(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(SweepInterval, timeProvider);

        try
        {
            do
            {
                await SweepAllAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task SweepAllAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<SystemScopeContext>().Activate();

        foreach (var sweep in scope.ServiceProvider.GetServices<IRetentionSweep>())
        {
            try
            {
                await sweep.SweepAsync(timeProvider.GetUtcNow(), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Retention sweep {Sweep} failed; retrying at the next interval", sweep.GetType().Name);
            }
        }
    }
}
