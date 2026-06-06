namespace Sentium.Sentinel.Application.Options;

/// <summary>
/// Static Policy Decision Point configuration bound from appsettings.
/// Runtime-tunables (lockdown, autonomy, rate limits, intent model) live in the Registry
/// and are managed via Sovereign controls. Only static policy lists and infrastructure
/// timeouts belong here.
/// </summary>
public sealed class PdpOptions
{
    public const string SectionName = "Pdp";

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

    public int IntentCheckTimeoutSeconds { get; set; } = 120;
}
