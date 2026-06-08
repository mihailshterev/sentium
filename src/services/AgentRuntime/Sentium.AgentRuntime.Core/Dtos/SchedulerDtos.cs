namespace Sentium.AgentRuntime.Core.Dtos;

public sealed record ScheduledJobResponse(
    string JobId,
    string AgentId,
    string JobName,
    string Language,
    string CronExpression,
    string? PreviousRun,
    string? NextRun,
    string Status);
