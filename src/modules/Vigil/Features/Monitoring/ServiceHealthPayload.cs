namespace Sentium.Vigil.Features.Monitoring;

public sealed record ServiceHealthPayload(
    string ServiceName,
    string Kind,
    string Status,
    double LatencyMs,
    double UptimePercent,
    string Timestamp,
    string? Details);
