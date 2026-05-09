namespace Sentium.AgentRuntime.Core.Entities;

/// <summary>
/// Singleton settings entity — one row per installation.
/// Holds the user-configurable global harness prompt and feature toggles for agent behaviour.
/// </summary>
public sealed class SystemSettings
{
    /// <summary>
    /// Well-known fixed ID so the row can be upserted without a prior lookup.
    /// </summary>
    public static readonly Guid WellKnownId = new("00000000-0000-0000-0000-000000000001");

    public Guid Id { get; set; } = WellKnownId;

    /// <summary>
    /// User-authored markdown/text prompt injected into every agent alongside the built-in harness.
    /// Empty by default — no injection occurs when blank.
    /// </summary>
    public string UserHarnessPrompt { get; set; } = string.Empty;

    /// <summary>
    /// When <c>true</c> (default) the built-in <see cref="Harness.UniversalSystemHarness.Policy"/> is prepended.
    /// Set to <c>false</c> to rely solely on the user-defined prompt.
    /// </summary>
    public bool IsBuiltInHarnessEnabled { get; set; } = true;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public string? UpdatedBy { get; set; }
}
