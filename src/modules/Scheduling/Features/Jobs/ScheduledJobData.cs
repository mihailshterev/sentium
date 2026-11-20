namespace Sentium.Scheduling.Features.Jobs;

/// <summary>
/// Keys of the <see cref="Quartz.JobDataMap"/> entries a Schedule carries. The job store runs with
/// <c>UseProperties = true</c>, so every value stored under these keys must be a string.
/// Ownership is not a data entry: the job's group is the owning user id (see <see cref="SchedulerDefaults.GroupFor"/>).
/// </summary>
internal static class ScheduledJobData
{
    public const string AgentId = "AgentId";
    public const string JobName = "JobName";
    public const string Code = "Code";
    public const string Language = "Language";

    public const string Kind = "Kind";
    public const string WorkflowId = "WorkflowId";

    /// <summary>
    /// The agent's or workflow's name when the Schedule was saved, for display after it is renamed or deleted.
    /// </summary>
    public const string TargetName = "TargetName";

    public const string Prompt = "Prompt";

    /// <summary>
    /// Pre-approved tool names, joined with <see cref="ToolSeparator"/>.
    /// </summary>
    public const string PreApprovedTools = "PreApprovedTools";

    public const string LastRunId = "LastRunId";
    public const string LastRunStatus = "LastRunStatus";
    public const string LastRunAt = "LastRunAt";

    /// <summary>
    /// Tool names never contain a line break, MCP ones included.
    /// </summary>
    public const char ToolSeparator = '\n';

    /// <summary>
    /// The entry's value, or <see langword="null"/> when the job has none: <c>JobDataMap.GetString</c>
    /// throws for a missing key, and jobs saved before an entry existed lack it.
    /// </summary>
    public static string? Find(this Quartz.JobDataMap data, string key)
    {
        ArgumentNullException.ThrowIfNull(data);

        return data.TryGetString(key, out var value) ? value : null;
    }
}
