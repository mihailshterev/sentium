using Quartz;
using Sentium.Praxis.Contracts;

namespace Sentium.Scheduling.Features.Jobs;

/// <summary>
/// Keeps each Schedule's last run on its job data, which is what the Scheduler page shows. A Schedule deleted while its
/// run was going has nothing to update.
/// </summary>
internal sealed class ScheduleRunRecorder(ISchedulerFactory schedulerFactory, ILogger<ScheduleRunRecorder> logger) : IUnattendedRunObserver
{
    public async Task OnRunCompletedAsync(UnattendedRunCompletion completion, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(completion);

        try
        {
            var scheduler = await schedulerFactory.GetScheduler(ct);
            var key = new JobKey(completion.ScheduleId, SchedulerDefaults.GroupFor(completion.OwnerId));

            if (await scheduler.GetJobDetail(key, ct) is not { } detail)
            {
                return;
            }

            var updated = detail.GetJobBuilder().Build();
            ScheduleLastRun.Write(updated.JobDataMap, completion);
            await scheduler.AddJob(updated, replace: true, storeNonDurableWhileAwaitingScheduling: true, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not record the last run of schedule {ScheduleId}.", completion.ScheduleId);
        }
    }
}
