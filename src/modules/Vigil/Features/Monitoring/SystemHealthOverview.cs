namespace Sentium.Vigil.Features.Monitoring;

internal sealed record SystemHealthOverview
{
    public required int Total { get; init; }
    public required int Healthy { get; init; }
    public required int Degraded { get; init; }
    public required int Unhealthy { get; init; }
    public required int Unknown { get; init; }
    public required ServiceStatus OverallStatus { get; init; }
    public required int OpenIncidents { get; init; }
    public required DateTimeOffset GeneratedAt { get; init; }
}
