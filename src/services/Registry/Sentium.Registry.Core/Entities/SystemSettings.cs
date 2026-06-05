using Sentium.Registry.Core.Settings;

namespace Sentium.Registry.Core.Entities;

public sealed class SystemSettings
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UserId { get; set; }
    public SettingsContainer Settings { get; set; } = new();
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
}
