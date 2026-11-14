using Sentium.Infrastructure.Security;

namespace Sentium.Tempus.Features.Reminders;

/// <summary>
/// Runs <see cref="TimelineReminderSweep"/> at startup and then every minute. The sweep has no user, so it runs in
/// a system scope; Tempus has no Sovereign bypass and would otherwise see no rows.
/// </summary>
internal sealed class TimelineReminderWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<TimelineReminderWorker> logger) : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(SweepInterval, timeProvider);

        try
        {
            do
            {
                await SweepAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<SystemScopeContext>().Activate();
            await scope.ServiceProvider.GetRequiredService<TimelineReminderSweep>().RunAsync(timeProvider.GetUtcNow(), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Timeline reminder sweep failed; retrying in a minute");
        }
    }
}
