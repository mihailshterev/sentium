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

    /// <summary>
    /// When <see langword="true"/> (the default), the <c>SensitiveDataEgress</c> policy scans
    /// outbound write/execute payloads for secrets and credentials and denies on a match.
    /// </summary>
    public bool EgressScanEnabled { get; set; } = true;

    /// <summary>
    /// High-confidence secret/credential regex patterns. A match in the resource id or any
    /// metadata value blocks the request. Defaults cover the most common credential shapes and
    /// are deliberately anchored to provider-specific prefixes to keep false positives low.
    /// </summary>
    public IReadOnlyList<string> SensitivePatterns { get; set; } =
    [
        "(?:AKIA|ASIA)[0-9A-Z]{16}",                                            // AWS access key id (long-term + temporary)
        "-----BEGIN (?:RSA |EC |OPENSSH |DSA |PGP )?PRIVATE KEY-----",          // PEM private key block
        "eyJ[A-Za-z0-9_-]{10,}\\.eyJ[A-Za-z0-9_-]{10,}\\.[A-Za-z0-9_-]{10,}",   // JWT (non-trivial segments)
        "sk-(?:proj-|svcacct-|admin-)?[A-Za-z0-9_-]{32,}",                      // OpenAI-style secret key
        "(?:sk|rk)_(?:live|test)_[A-Za-z0-9]{16,}",                             // Stripe secret / restricted key
        "gh[pousr]_[A-Za-z0-9]{36,}",                                           // GitHub token (PAT / OAuth / user / server / refresh)
        "xox[baprs]-[A-Za-z0-9-]{10,}"                                          // Slack token
    ];
}
