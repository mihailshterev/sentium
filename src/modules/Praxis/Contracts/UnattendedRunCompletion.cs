namespace Sentium.Praxis.Contracts;

/// <param name="RunId">The run's history row, or <see langword="null"/> when it could not be persisted.</param>
public sealed record UnattendedRunCompletion(string ScheduleId, Guid OwnerId, Guid? RunId, UnattendedRunStatus Status, DateTimeOffset CompletedAt);
