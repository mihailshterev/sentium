namespace Sentium.Scheduling.Features.Jobs;

/// <summary>
/// Scheduler-wide constants. Changing <see cref="SchedulerName"/> or <see cref="TablePrefix"/> orphans every persisted job.
/// </summary>
internal static class SchedulerDefaults
{
    /// <summary>
    /// Stored in every Quartz row (<c>sched_name</c>); must stay stable across releases.
    /// </summary>
    public const string SchedulerName = "sentium-scheduler";

    /// <summary>
    /// Quartz tables live in the Praxis schema; created by the <c>AddQuartzJobStore</c> migration.
    /// </summary>
    public const string TablePrefix = "praxis.qrtz_";

    /// <summary>
    /// Shortest gap allowed between two fires of an agent-scheduled job. Every fire starts a sandbox
    /// container, so per-second schedules would be a resource-exhaustion vector.
    /// </summary>
    public static readonly TimeSpan MinimumInterval = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Maximum active scheduled jobs a single user may own.
    /// </summary>
    public const int MaxJobsPerUser = 25;

    /// <summary>
    /// Job and trigger group of a user's scheduled jobs. Grouping by owner makes ownership part of the
    /// <see cref="Quartz.JobKey"/>, so a user can only address their own jobs and listing them is one query.
    /// </summary>
    public static string GroupFor(Guid userId) => userId.ToString("D");
}
