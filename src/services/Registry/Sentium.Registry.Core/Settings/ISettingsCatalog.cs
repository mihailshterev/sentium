using System.Diagnostics.CodeAnalysis;

namespace Sentium.Registry.Core.Settings;

/// <summary>
/// Registry of known settings keys, used to resolve a key to its <see cref="ISettingsDescriptor"/>.
/// </summary>
public interface ISettingsCatalog
{
    bool TryGet(string key, [NotNullWhen(true)] out ISettingsDescriptor? descriptor);
}
