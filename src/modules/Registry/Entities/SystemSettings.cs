using Sentium.Registry.Features.Settings;

namespace Sentium.Registry.Entities;

internal sealed class SystemSettings
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UserId { get; set; }
    public SettingsContainer Settings { get; set; } = new();
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }

    /// <summary>
    /// PostgreSQL's <c>xmin</c>: every key lives in this one row, so concurrent updates of different keys must not
    /// overwrite each other's change.
    /// </summary>
    public uint Version { get; set; }
}
