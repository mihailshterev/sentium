namespace Sentium.Watchdog.Core.Monitoring;

public enum IncidentStatus
{
    Open,
    Resolved
}

public enum IncidentSeverity
{
    Warning,
    Critical
}

public sealed record Incident
{
    public required Guid Id { get; init; }
    public required string Target { get; init; }
    public required ComponentKind Kind { get; init; }
    public required IncidentSeverity Severity { get; init; }
    public required IncidentStatus Status { get; init; }
    public required DateTimeOffset OpenedAt { get; init; }
    public DateTimeOffset? ResolvedAt { get; init; }
    public double? DurationMs { get; init; }
    public string? Description { get; init; }
    public required ServiceStatus LastObservedStatus { get; init; }
}
