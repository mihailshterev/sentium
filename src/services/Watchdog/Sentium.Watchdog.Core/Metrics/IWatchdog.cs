namespace Sentium.Watchdog.Core.Metrics;

/// <summary>
/// Provides a point-in-time snapshot of host system metrics (CPU, memory, etc.).
/// </summary>
public interface IWatchdog
{
    SystemMetrics GetMetrics();
}
