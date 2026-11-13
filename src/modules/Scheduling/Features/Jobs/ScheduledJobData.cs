namespace Sentium.Scheduling.Features.Jobs;

/// <summary>
/// Keys of the <see cref="Quartz.JobDataMap"/> entries an agent-scheduled <see cref="ExecuteJob"/> carries.
/// The job store runs with <c>UseProperties = true</c>, so every value stored under these keys must be a string.
/// Ownership is not a data entry: the job's group is the owning user id (see <see cref="SchedulerDefaults.GroupFor"/>).
/// </summary>
public static class ScheduledJobData
{
    public const string AgentId = "AgentId";
    public const string JobName = "JobName";
    public const string Code = "Code";
    public const string Language = "Language";
}

/// <summary>
/// Scheduler-wide constants. Changing <see cref="SchedulerName"/> or <see cref="TablePrefix"/> orphans every persisted job.
/// </summary>
public static class SchedulerDefaults
{
    /// <summary>Stored in every Quartz row (<c>sched_name</c>); must stay stable across releases.</summary>
    public const string SchedulerName = "sentium-scheduler";

    /// <summary>Quartz tables live in the Praxis schema; created by the <c>AddQuartzJobStore</c> migration.</summary>
    public const string TablePrefix = "praxis.qrtz_";

    /// <summary>
    /// Shortest gap allowed between two fires of an agent-scheduled job. Every fire starts a sandbox
    /// container, so per-second schedules would be a resource-exhaustion vector.
    /// </summary>
    public static readonly TimeSpan MinimumInterval = TimeSpan.FromMinutes(1);

    /// <summary>Maximum active scheduled jobs a single user may own.</summary>
    public const int MaxJobsPerUser = 25;

    /// <summary>
    /// Job and trigger group of a user's scheduled jobs. Grouping by owner makes ownership part of the
    /// <see cref="Quartz.JobKey"/>, so a user can only address their own jobs and listing them is one query.
    /// </summary>
    public static string GroupFor(Guid userId) => userId.ToString("D");
}
