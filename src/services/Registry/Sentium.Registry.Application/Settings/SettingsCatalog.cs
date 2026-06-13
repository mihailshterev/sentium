using System.Diagnostics.CodeAnalysis;
using Sentium.Registry.Core.Settings;

namespace Sentium.Registry.Application.Settings;

public sealed class SettingsCatalog : ISettingsCatalog
{
    private readonly Dictionary<string, ISettingsDescriptor> _byKey;

    public SettingsCatalog(IEnumerable<ISettingsDescriptor> descriptors)
    {
        ArgumentNullException.ThrowIfNull(descriptors);
        _byKey = descriptors.ToDictionary(d => d.Key, StringComparer.OrdinalIgnoreCase);
    }

    public bool TryGet(string key, [NotNullWhen(true)] out ISettingsDescriptor? descriptor) => _byKey.TryGetValue(key, out descriptor);
}
