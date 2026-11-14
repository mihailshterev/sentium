using Sentium.Infrastructure.Security;

namespace Sentium.Tempus.Features.Series;

/// <summary>
/// Runs <see cref="TimelineSeriesMaterializer"/> at startup and then daily, in a system scope (it has no user, and
/// Tempus has no Sovereign bypass).
/// </summary>
internal sealed class TimelineSeriesWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<TimelineSeriesWorker> logger) : BackgroundService
{
    private static readonly TimeSpan RunInterval = TimeSpan.FromDays(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(RunInterval, timeProvider);

        try
        {
            do
            {
                await MaterializeAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task MaterializeAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<SystemScopeContext>().Activate();
            await scope.ServiceProvider.GetRequiredService<TimelineSeriesMaterializer>().RunAsync(timeProvider.GetUtcNow(), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Timeline Series materialisation failed; retrying tomorrow");
        }
    }
}
