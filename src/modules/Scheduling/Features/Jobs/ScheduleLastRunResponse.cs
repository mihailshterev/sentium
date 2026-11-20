namespace Sentium.Scheduling.Features.Jobs;

/// <param name="Status">An <c>UnattendedRunStatus</c> name: Completed, Failed, TimedOut, Interrupted or Cancelled.</param>
internal sealed record ScheduleLastRunResponse(Guid? RunId, string Status, DateTimeOffset At);
