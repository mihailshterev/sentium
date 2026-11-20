using System.ComponentModel.DataAnnotations;
using Sentium.Core.Constants;

namespace Sentium.Registry.Contracts.Settings;

/// <summary>
/// Runtime-configurable Vigil monitoring settings, managed by Sovereign users and consumed by
/// the Vigil module. Controls poll cadence, probe timeouts, the degraded-latency threshold, and
/// when a sustained degradation escalates into an incident.
/// </summary>
[SettingsGroup(
    SettingsKeys.Vigil,
    SettingsScope.Household,
    "monitoring",
    "Monitoring",
    "How Vigil probes services and infrastructure and when it opens an incident.",
    Order = 5)]
public sealed class VigilSettings
{
    [Setting(
        "Poll interval",
        "How often every target is probed.",
        Keywords = ["vigil", "health", "probe", "frequency"],
        Unit = "s")]
    [Range(5, 3_600)]
    public int PollIntervalSeconds { get; set; } = 15;

    [Setting(
        "Probe timeout",
        "Time allowed for a single probe. Can't exceed the poll interval.",
        Keywords = ["vigil", "health", "timeout"],
        Unit = "s")]
    [Range(1, 60)]
    public int ProbeTimeoutSeconds { get; set; } = 5;

    [Setting(
        "Degraded latency",
        "A reachable target slower than this is reported as Degraded.",
        Keywords = ["vigil", "health", "latency", "slow"],
        Unit = "ms")]
    [Range(1, 60_000)]
    public int DegradedLatencyMs { get; set; } = 1_000;

    /// <summary>
    /// Counts consecutive non-healthy cycles of a Degraded (but reachable) target. Unhealthy targets always open an
    /// incident immediately.
    /// </summary>
    [Setting(
        "Failures before an incident",
        "Consecutive Degraded cycles before an incident opens. Unhealthy targets always open one immediately.",
        Keywords = ["vigil", "health", "incident", "alert"],
        Unit = "cycles")]
    [Range(1, 100)]
    public int ConsecutiveFailuresToOpenIncident { get; set; } = 3;

    [Setting(
        "Sample history",
        "Recent samples kept per target for the sparklines.",
        Keywords = ["vigil", "health", "sparkline", "history"],
        Unit = "samples")]
    [Range(10, 200)]
    public int SampleHistorySize { get; set; } = 60;

    /// <summary>
    /// Devices learn the interval from the response to each Report, so a change reaches them within one interval.
    /// </summary>
    [Setting(
        "Device report interval",
        "How often a paired PC reports its health. A device shows Offline after three missed reports.",
        Keywords = ["vigil", "device", "satelles", "tray", "pc", "report", "offline"],
        Unit = "s")]
    [Range(10, 3_600)]
    public int DeviceReportIntervalSeconds { get; set; } = 30;
}
