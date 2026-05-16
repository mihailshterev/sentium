namespace Sentium.Sentinel.Application.Options;

/// <summary>
/// Configuration for the Policy Decision Point engine.
/// Bind from <c>appsettings.json</c> under the key <c>"Pdp"</c>.
/// </summary>
public sealed class PdpOptions
{
    public const string SectionName = "Pdp";
    public int RateLimitMaxRequests { get; set; } = 120;
    public int RateLimitWindowSeconds { get; set; } = 60;
    public IReadOnlyList<string> ForbiddenActions { get; set; } =
    [
        "delete",
        "drop",
        "truncate",
        "purge",
        "override",
        "bypass",
        "escalate"
    ];

    public IReadOnlyList<string> ProtectedResourcePrefixes { get; set; } =
    [
        "/etc/",
        "/sys/",
        "/proc/",
        "C:\\Windows\\",
        ".env",
        "appsettings",
        "secret",
        "private_key"
    ];

    public bool LockdownMode { get; set; } = false;
    public int AutonomyLevel { get; set; } = 5;
    public bool SemanticIntentCheckEnabled { get; set; } = true;
    public string IntentCheckModel { get; set; } = "llama3.2:1b";
    public int IntentCheckTimeoutSeconds { get; set; } = 120;
}
