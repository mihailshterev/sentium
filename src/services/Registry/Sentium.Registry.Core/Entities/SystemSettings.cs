using Sentium.Registry.Core.Settings;

namespace Sentium.Registry.Core.Entities;

/// <summary>
/// Singleton entity — exactly one row per installation.
/// The entire <see cref="Settings"/> object graph is stored as a single JSON column,
/// so new configuration sections can be added without DB migrations.
/// </summary>
public sealed class SystemSettings
{
    /// <summary>Fixed ID so the row can always be upserted without a prior lookup.</summary>
    public static readonly Guid WellKnownId = new("00000000-0000-0000-0000-000000000010");

    public Guid Id { get; set; } = WellKnownId;

    public SettingsContainer Settings { get; set; } = new();

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public string? UpdatedBy { get; set; }
}
