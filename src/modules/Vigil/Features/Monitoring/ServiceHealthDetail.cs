namespace Sentium.Vigil.Features.Monitoring;

public sealed record ServiceHealthDetail(ServiceHealthStatus Status, IReadOnlyList<HealthSample> Samples);
