namespace Sentium.Vigil.Features.Monitoring;

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
