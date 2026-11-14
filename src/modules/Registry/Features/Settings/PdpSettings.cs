namespace Sentium.Registry.Features.Settings;

/// <summary>
/// Runtime-configurable Policy Decision Point settings, managed by Sovereign users and consumed
/// by the Sentinel module. Static policy lists (forbidden actions, protected prefixes) remain in
/// the <c>Pdp</c> configuration section (Sentinel's <c>PdpOptions</c>).
/// </summary>
internal sealed class PdpSettings
{
    public bool LockdownMode { get; set; } = false;
    public int AutonomyLevel { get; set; } = 5;
    public bool SemanticIntentCheckEnabled { get; set; } = true;
    public string IntentCheckModel { get; set; } = string.Empty;
    public int RateLimitMaxRequests { get; set; } = 120;
    public int RateLimitWindowSeconds { get; set; } = 60;
}
