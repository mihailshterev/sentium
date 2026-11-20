namespace Sentium.Scheduling.Features.Jobs;

/// <param name="AgentId">For a script Schedule the agent that created it; for an agent Schedule the agent it runs.</param>
/// <param name="TargetName">The agent's or workflow's name when the Schedule was saved; empty for a script Schedule.</param>
/// <param name="TimeZone">The zone <paramref name="CronExpression"/> is read in.</param>
internal sealed record ScheduledJobResponse(
    string JobId,
    string AgentId,
    string JobName,
    string Language,
    string CronExpression,
    string? PreviousRun,
    string? NextRun,
    string Status,
    ScheduleKind Kind = ScheduleKind.Script,
    Guid? WorkflowId = null,
    string TargetName = "",
    string Prompt = "",
    IReadOnlyList<string>? PreApprovedTools = null,
    string TimeZone = "UTC",
    bool Paused = false,
    ScheduleLastRunResponse? LastRun = null);
