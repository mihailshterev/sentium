namespace Sentium.AgentRuntime.Core.Scheduling;

public sealed record ScheduleAgentJobRequest(
    string AgentId,
    string JobName,
    string CronExpression,
    string Code,
    ScheduledJobLanguage Language
);
