using System.Diagnostics.CodeAnalysis;

namespace Sentium.Registry.Core.Settings;

public interface ISettingsCatalog
{
    bool TryGet(string key, [NotNullWhen(true)] out ISettingsDescriptor? descriptor);
}
