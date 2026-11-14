namespace Sentium.Scheduling.Features.Jobs;

/// <summary>
/// Keys of the <see cref="Quartz.JobDataMap"/> entries an agent-scheduled <see cref="ExecuteJob"/> carries.
/// The job store runs with <c>UseProperties = true</c>, so every value stored under these keys must be a string.
/// Ownership is not a data entry: the job's group is the owning user id (see <see cref="SchedulerDefaults.GroupFor"/>).
/// </summary>
internal static class ScheduledJobData
{
    public const string AgentId = "AgentId";
    public const string JobName = "JobName";
    public const string Code = "Code";
    public const string Language = "Language";
}
