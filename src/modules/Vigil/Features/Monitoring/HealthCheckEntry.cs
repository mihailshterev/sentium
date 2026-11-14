namespace Sentium.Vigil.Features.Monitoring;

public sealed record HealthCheckEntry(
    string Name,
    string Status,
    string? Description,
    double DurationMs);
