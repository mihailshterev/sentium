using System.Text.Json;

namespace Sentium.Registry.Entities;

internal sealed class SystemSettings
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UserId { get; set; }
    /// <summary>
    /// Each settings key's value as JSON, under the key itself. Keys the catalog no longer knows are kept as they are.
    /// </summary>
    public Dictionary<string, JsonElement> Settings { get; set; } = [];
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }

    /// <summary>
    /// PostgreSQL's <c>xmin</c>: every key lives in this one row, so concurrent updates of different keys must not
    /// overwrite each other's change.
    /// </summary>
    public uint Version { get; set; }
}
