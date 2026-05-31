namespace Sentium.Registry.Core.Settings;

/// <summary>
/// Root JSON payload stored in the SystemSettings.Settings column.
/// Add new top-level sections (Monitoring, Security, etc.) here without any DB migration.
/// </summary>
public sealed class SettingsContainer
{
    public HarnessSettings Harness { get; set; } = new();
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
}
