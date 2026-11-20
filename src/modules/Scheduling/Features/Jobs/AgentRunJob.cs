using Quartz;
using Sentium.Praxis.Contracts;

namespace Sentium.Scheduling.Features.Jobs;

/// <summary>
/// Fires an agent or workflow Schedule: hands the run to Praxis, which queues it as the owner and later reports how it
/// ended (see <see cref="ScheduleRunRecorder"/>). The job itself returns at once, so a fire that arrives while the
/// previous run is still going is skipped by the launcher rather than delayed by Quartz. Failures are logged, never
/// rethrown: a refire would only repeat them.
/// </summary>
internal sealed class AgentRunJob(IUnattendedRunLauncher launcher, ILogger<AgentRunJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var key = context.JobDetail.Key;
        if (!Guid.TryParse(key.Group, out var ownerId) || ScheduleDefinition.Read(context.MergedJobDataMap) is not { } schedule)
        {
            logger.LogError("Schedule {JobKey} has no owner or is not an agent or workflow Schedule; nothing was run.", key);
            return;
        }

        // The power-aware-inference gate (deferring runs to a quiet window) belongs here, before the launch.

        try
        {
            var launch = await launcher.LaunchAsync(schedule.ToRunRequest(key.Name, ownerId), context.CancellationToken);

            if (launch == UnattendedRunLaunch.SkippedStillRunning)
            {
                logger.LogInformation("Skipped a fire of schedule {JobKey}: its previous run has not ended.", key);
            }
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Schedule {JobKey} was interrupted by scheduler shutdown before its run was queued.", key);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not start the run of schedule {JobKey}.", key);
        }
    }
}
