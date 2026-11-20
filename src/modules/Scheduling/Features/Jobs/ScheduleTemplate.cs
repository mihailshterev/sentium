namespace Sentium.Scheduling.Features.Jobs;

/// <summary>
/// A ready-made starting point for an agent Schedule; the owner can change every field before saving.
/// </summary>
internal sealed record ScheduleTemplate(string Id, string Name, string Description, string AgentName, string CronExpression, string Prompt);
