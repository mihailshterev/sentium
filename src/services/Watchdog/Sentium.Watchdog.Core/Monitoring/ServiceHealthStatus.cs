namespace Sentium.Watchdog.Core.Monitoring;

public enum ServiceStatus
{
    Unknown,
    Healthy,
    Degraded,
    Unhealthy
}

public enum ComponentKind
{
    Service,
    Infrastructure
}

public sealed record HealthCheckEntry(
    string Name,
    string Status,
    string? Description,
    double DurationMs);

public sealed record HealthSample(
    DateTimeOffset At,
    ServiceStatus Status,
    double LatencyMs);

public sealed record ServiceHealthStatus
{
    public required string ServiceName { get; init; }
    public ComponentKind Kind { get; init; } = ComponentKind.Service;
    public required ServiceStatus Status { get; init; }
    public required double LatencyMs { get; init; }
    public required DateTimeOffset CheckedAt { get; init; }
    public string? Details { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<HealthCheckEntry> Checks { get; init; } = [];
    public double UptimePercent { get; init; }
    public DateTimeOffset LastStateChange { get; init; }
    public int ConsecutiveFailures { get; init; }
}

public sealed record ServiceHealthPayload(
    string ServiceName,
    string Kind,
    string Status,
    double LatencyMs,
    double UptimePercent,
    string Timestamp,
    string? Details);
