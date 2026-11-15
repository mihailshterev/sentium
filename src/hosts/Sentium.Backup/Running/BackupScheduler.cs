using Microsoft.Extensions.Options;
using Sentium.Backup.Options;
using Sentium.Backup.Status;

namespace Sentium.Backup.Running;

/// <summary>
/// The nightly loop. It lives in the sidecar rather than in Quartz so backups keep running while Sentium.Web is down.
/// </summary>
internal sealed class BackupScheduler(
    BackupCoordinator coordinator,
    IOptions<BackupOptions> options,
    TimeProvider time,
    ILogger<BackupScheduler> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await coordinator.InitializeAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var next = BackupSchedule.NextOccurrence(time.GetUtcNow(), options.Value.TimeOfDay, time.LocalTimeZone);
            coordinator.NextRunAt = next;
            logger.LogInformation("Next backup at {NextRunAt:O}", next);

            try
            {
                var wait = next - time.GetUtcNow();
                await Task.Delay(wait > TimeSpan.Zero ? wait : TimeSpan.Zero, time, stoppingToken);
                await coordinator.RunAsync(BackupTriggers.Scheduled, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }
}
