namespace Sentium.Vigil.Features.Monitoring;

public sealed record HealthSample(
    DateTimeOffset At,
    ServiceStatus Status,
    double LatencyMs);
