using System.Diagnostics.CodeAnalysis;

namespace Sentium.Registry.Features.Settings;

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
