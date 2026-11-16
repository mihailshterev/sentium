using Quartz;
using Quartz.Impl.Matchers;
using Sentium.Identity.Contracts;

namespace Sentium.Scheduling.Features.Jobs;

/// <summary>
/// Unschedules a removed user's jobs, which would otherwise keep running their code in the Sandbox.
/// </summary>
internal sealed class ScheduledJobRemover(ISchedulerFactory schedulerFactory, ILogger<ScheduledJobRemover> logger) : IUserRemovalObserver
{
    public async Task OnUserRemovedAsync(Guid userId, CancellationToken ct)
    {
        var scheduler = await schedulerFactory.GetScheduler(ct);
        var jobs = await scheduler.GetJobKeys(GroupMatcher<JobKey>.GroupEquals(SchedulerDefaults.GroupFor(userId)), ct);

        if (jobs.Count == 0)
        {
            return;
        }

        await scheduler.DeleteJobs([.. jobs], ct);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Deleted {Count} scheduled jobs of removed user {UserId}.", jobs.Count, userId);
        }
    }
}
