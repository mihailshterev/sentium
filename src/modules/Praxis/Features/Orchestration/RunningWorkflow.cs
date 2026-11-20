namespace Sentium.Praxis.Features.Orchestration;

internal sealed record RunningWorkflow(string StreamId, Guid? OwnerId, string? Label, DateTimeOffset StartedAt);
