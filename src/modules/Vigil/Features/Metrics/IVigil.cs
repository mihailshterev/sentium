namespace Sentium.Vigil.Features.Metrics;

/// <summary>
/// Provides a point-in-time snapshot of host system metrics (CPU, memory, etc.).
/// </summary>
public interface IVigil
{
    SystemMetrics GetMetrics();
}
