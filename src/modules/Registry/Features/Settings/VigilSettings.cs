namespace Sentium.Registry.Features.Settings;

/// <summary>
/// Runtime-configurable Vigil monitoring settings, managed by Sovereign users and consumed by
/// the Vigil module. Controls poll cadence, probe timeouts, the degraded-latency threshold, and
/// when a sustained degradation escalates into an incident.
/// </summary>
public sealed class VigilSettings
{
    /// <summary>
    /// How often every target is probed, in seconds.
    /// </summary>
    public int PollIntervalSeconds { get; set; } = 15;

    public int ProbeTimeoutSeconds { get; set; } = 5;

    /// <summary>
    /// A reachable target whose latency exceeds this is reported as Degraded.
    /// </summary>
    public int DegradedLatencyMs { get; set; } = 1_000;

    /// <summary>
    /// Number of consecutive non-healthy cycles a Degraded (but reachable) target must sustain
    /// before an incident is opened. Unhealthy targets always open an incident immediately.
    /// </summary>
    public int ConsecutiveFailuresToOpenIncident { get; set; } = 3;

    /// <summary>
    /// Maximum number of recent samples returned per target for sparklines.
    /// </summary>
    public int SampleHistorySize { get; set; } = 60;
}
