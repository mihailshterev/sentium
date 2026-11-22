using Quartz;
using Sentium.Infrastructure.Realtime;

namespace Sentium.Scheduling.Features.Jobs;

/// <summary>
/// Tells the owner's portal sessions when one of their jobs starts or finishes, so the Scheduler page shows its state
/// and last run without polling. A job's group is its owner (see <see cref="SchedulerDefaults.GroupFor"/>); jobs in
/// any other group belong to nobody in particular and publish nothing.
/// </summary>
internal sealed class ScheduleChangeListener(IRealtimePublisher realtime) : IJobListener
{
    public string Name => nameof(ScheduleChangeListener);

    public Task JobToBeExecuted(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        Publish(context);
        return Task.CompletedTask;
    }

    public Task JobExecutionVetoed(IJobExecutionContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task JobWasExecuted(IJobExecutionContext context, JobExecutionException? jobException, CancellationToken cancellationToken = default)
    {
        Publish(context);
        return Task.CompletedTask;
    }

    private void Publish(IJobExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (Guid.TryParse(context.JobDetail.Key.Group, out var ownerId))
        {
            realtime.Publish(new RealtimeEvent(RealtimeTopics.Schedules, RealtimeAudience.User(ownerId)));
        }
    }
}
