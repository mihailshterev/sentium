using System.Text.Json;

namespace Sentium.Registry.Core.Settings;

/// <summary>
/// Describes a single typed settings key: its scope and how to read, write, deserialize, and validate its value.
/// </summary>
public interface ISettingsDescriptor
{
    string Key { get; }
    SettingsScope Scope { get; }

    /// <summary>
    /// Reads this key's value out of the settings container.
    /// </summary>
    object Read(SettingsContainer container);

    /// <summary>
    /// Writes this key's value into the settings container.
    /// </summary>
    void Write(SettingsContainer container, object value);

    /// <summary>
    /// Deserializes raw JSON into the strongly-typed value for this key.
    /// </summary>
    object Deserialize(JsonElement json);

    /// <summary>
    /// Validates a value before it is persisted, throwing if it is invalid.
    /// </summary>
    Task ValidateAsync(object value, IServiceProvider serviceProvider, CancellationToken ct = default);
}
