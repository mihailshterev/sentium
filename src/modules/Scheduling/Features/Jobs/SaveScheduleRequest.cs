namespace Sentium.Scheduling.Features.Jobs;

/// <summary>
/// Creates or edits a Schedule. Editing a script Schedule changes only its name and cadence.
/// </summary>
/// <param name="CronExpression">Quartz cron, read in the owner's time zone.</param>
/// <param name="PreApprovedTools">
/// Tools the owner allows the run to use without approval; each must be an approval-gated tool of the agent or workflow.
/// On an edit, <see langword="null"/> keeps the current ones, unless the agent, workflow or prompt changed, which clears them.
/// </param>
internal sealed record SaveScheduleRequest(
    string Name,
    ScheduleKind Kind,
    string CronExpression,
    string? AgentName = null,
    Guid? WorkflowId = null,
    string? Prompt = null,
    IReadOnlyList<string>? PreApprovedTools = null);
