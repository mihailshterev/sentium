using System.Text.Json;

namespace Sentium.Registry.Core.Settings;

public interface ISettingsDescriptor
{
    string Key { get; }
    SettingsScope Scope { get; }
    object Read(SettingsContainer container);
    void Write(SettingsContainer container, object value);
    object Deserialize(JsonElement json);
    Task ValidateAsync(object value, IServiceProvider serviceProvider, CancellationToken ct = default);
}
