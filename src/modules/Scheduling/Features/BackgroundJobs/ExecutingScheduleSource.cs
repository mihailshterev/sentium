using Quartz;
using Sentium.Infrastructure.BackgroundWork;
using Sentium.Scheduling.Features.Jobs;

namespace Sentium.Scheduling.Features.BackgroundJobs;

/// <summary>
/// Quartz jobs executing right now. An agent or workflow Schedule only hands its run to the workflow queue, so it shows
/// here for a moment and then as a workflow run; a script job stays here while the Sandbox runs it.
/// </summary>
internal sealed class ExecutingScheduleSource(ISchedulerFactory schedulerFactory) : IBackgroundActivitySource
{
    private const string Source = "Scheduled jobs";

    public async Task<IReadOnlyList<BackgroundActivity>> GetActivitiesAsync(CancellationToken ct)
    {
        var scheduler = await schedulerFactory.GetScheduler(ct);
        var executing = await scheduler.GetCurrentlyExecutingJobs(ct);

        return
        [
            .. executing.Select(job => new BackgroundActivity(
                Source,
                job.JobDetail.JobDataMap.Find(ScheduledJobData.JobName) ?? job.JobDetail.Key.Name,
                ScheduleDefinition.ReadKind(job.JobDetail.JobDataMap).ToString(),
                Guid.TryParse(job.JobDetail.Key.Group, out var ownerId) ? ownerId : null,
                job.FireTimeUtc))
        ];
    }
}
