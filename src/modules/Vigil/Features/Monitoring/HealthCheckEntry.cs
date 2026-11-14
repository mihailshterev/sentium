namespace Sentium.Vigil.Features.Monitoring;

internal sealed record HealthCheckEntry(
    string Name,
    string Status,
    string? Description,
    double DurationMs);
