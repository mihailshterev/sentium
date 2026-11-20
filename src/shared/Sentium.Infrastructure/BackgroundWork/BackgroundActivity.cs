namespace Sentium.Infrastructure.BackgroundWork;

/// <summary>
/// One piece of background work in progress right now, such as a workflow run or an executing scheduled job.
/// </summary>
/// <param name="Source">What produced it, for grouping (for example "Workflow runs").</param>
/// <param name="OwnerId">The user it runs for; <see langword="null"/> for system work.</param>
public sealed record BackgroundActivity(string Source, string Name, string? Detail, Guid? OwnerId, DateTimeOffset? StartedAt);
