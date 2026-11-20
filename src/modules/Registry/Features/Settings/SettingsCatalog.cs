using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Sentium.Registry.Contracts.Settings;

namespace Sentium.Registry.Features.Settings;

internal sealed class SettingsCatalog : ISettingsCatalog
{
    private readonly FrozenDictionary<string, ISettingsDescriptor> _byKey;

    public SettingsCatalog(IEnumerable<ISettingsDescriptor> descriptors)
    {
        ArgumentNullException.ThrowIfNull(descriptors);
        _byKey = descriptors.ToFrozenDictionary(d => d.Key, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<ISettingsDescriptor> All => _byKey.Values;

    /// <summary>
    /// Every <see cref="SettingsGroupAttribute"/> contract in <paramref name="assembly"/>. Two groups with the same
    /// key throw.
    /// </summary>
    public static SettingsCatalog FromAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var descriptors = assembly.GetTypes()
            .Select(type => (Type: type, Group: type.GetCustomAttribute<SettingsGroupAttribute>()))
            .Where(entry => entry.Group is not null)
            .Select(entry => CreateDescriptor(entry.Type, entry.Group!));

        return new SettingsCatalog(descriptors);
    }

    public bool TryGet(string key, [NotNullWhen(true)] out ISettingsDescriptor? descriptor) => _byKey.TryGetValue(key, out descriptor);

    private static ISettingsDescriptor CreateDescriptor(Type contract, SettingsGroupAttribute group)
    {
        var descriptorType = typeof(SettingsDescriptor<>).MakeGenericType(contract);
        return (ISettingsDescriptor)Activator.CreateInstance(descriptorType, group.Key, group.Scope)!;
    }
}
