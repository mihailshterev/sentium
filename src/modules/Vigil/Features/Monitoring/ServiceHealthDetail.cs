namespace Sentium.Vigil.Features.Monitoring;

internal sealed record ServiceHealthDetail(ServiceHealthStatus Status, IReadOnlyList<HealthSample> Samples);
