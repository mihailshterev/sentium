namespace Sentium.Registry.Core.Settings;

/// <summary>
/// Root JSON payload stored in the SystemSettings.Settings column.
/// </summary>
public sealed class SettingsContainer
{
    /// <summary>
    /// Per-user agent harness settings.
    /// </summary>
    public HarnessSettings Harness { get; set; } = new();

    /// <summary>
    /// Global Policy Decision Point settings.
    /// </summary>
    public PdpSettings Pdp { get; set; } = new();

    /// <summary>
    /// Global Ollama inference settings.
    /// </summary>
    public OllamaSettings Ollama { get; set; } = new();

    /// <summary>
    /// Global Watchdog monitoring settings.
    /// </summary>
    public WatchdogSettings Watchdog { get; set; } = new();
}

/// <summary>
/// Controls the global system-prompt harness prepended to every agent inference call.
/// </summary>
public sealed class HarnessSettings
{
    /// <summary>
    /// User-authored text injected into every agent alongside the built-in policy.
    /// Empty string means no injection.
    /// </summary>
    public string UserHarnessPrompt { get; set; } = string.Empty;

    /// <summary>
    /// When true (default) the built-in <c>UniversalSystemHarness.Policy</c> is prepended.
    /// Set to false to rely solely on <see cref="UserHarnessPrompt"/>.
    /// </summary>
    public bool IsBuiltInHarnessEnabled { get; set; } = true;

    /// <summary>
    /// When true the user's prompt is rewritten by a fast pre-execution enhancement pass
    /// before the agent runs, to improve results from smaller local models.
    /// </summary>
    public bool IsPromptEnhancementEnabled { get; set; } = false;
}

/// <summary>
/// Runtime-configurable Policy Decision Point settings, managed by Sovereign users and consumed
/// by the Sentinel service. Static policy lists (forbidden actions, protected prefixes) remain in
/// Sentinel's appsettings.
/// </summary>
public sealed class PdpSettings
{
    public bool LockdownMode { get; set; } = false;
    public int AutonomyLevel { get; set; } = 5;
    public bool SemanticIntentCheckEnabled { get; set; } = true;
    public string IntentCheckModel { get; set; } = string.Empty;
    public int RateLimitMaxRequests { get; set; } = 120;
    public int RateLimitWindowSeconds { get; set; } = 60;
}

/// <summary>
/// Runtime-configurable Watchdog monitoring settings, managed by Sovereign users and consumed by
/// the Watchdog service. Controls poll cadence, probe timeouts, the degraded-latency threshold, and
/// when a sustained degradation escalates into an incident.
/// </summary>
public sealed class WatchdogSettings
{
    /// <summary>
    /// How often every target is probed, in seconds.
    /// </summary>
    public int PollIntervalSeconds { get; set; } = 15;

    /// <summary>
    /// Per-probe timeout, in seconds.
    /// </summary>
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
