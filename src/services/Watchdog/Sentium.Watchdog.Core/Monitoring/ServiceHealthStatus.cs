namespace Sentium.Watchdog.Core.Monitoring;

public enum ServiceStatus
{
    Unknown,
    Healthy,
    Unhealthy
}

public sealed record ServiceHealthStatus
{
    public required string ServiceName { get; init; }
    public required ServiceStatus Status { get; init; }
    public required double LatencyMs { get; init; }
    public required DateTimeOffset CheckedAt { get; init; }
    public string? Details { get; init; }
}

public sealed record ServiceHealthPayload(
    string ServiceName,
    string Status,
    double LatencyMs,
    string Timestamp,
    string? Details);
