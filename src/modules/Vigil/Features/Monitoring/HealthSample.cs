namespace Sentium.Vigil.Features.Monitoring;

internal sealed record HealthSample(
    DateTimeOffset At,
    ServiceStatus Status,
    double LatencyMs);
