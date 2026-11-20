using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Sentium.Registry.Contracts.Settings;

namespace Sentium.Registry.Features.Settings;

/// <summary>
/// Builds the Settings page schema once from the catalog's contracts: <see cref="SettingsGroupAttribute"/> makes a
/// key a section, <see cref="SettingAttribute"/> makes a property a field, DataAnnotations supply the bounds and the
/// contract's initializers the defaults. A contract that can't be rendered throws on first use, which the schema
/// tests catch.
/// </summary>
internal sealed class SettingsSchemaProvider(ISettingsCatalog catalog)
{
    public const string PersonalScope = "personal";
    public const string HouseholdScope = "household";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly Lazy<IReadOnlyList<SettingsSectionSchema>> _sections = new(() => Build(catalog));

    public IReadOnlyList<SettingsSectionSchema> Sections => _sections.Value;

    private static List<SettingsSectionSchema> Build(ISettingsCatalog catalog)
    {
        var groups = catalog.All
            .Select(descriptor => (Descriptor: descriptor, Group: descriptor.ValueType.GetCustomAttribute<SettingsGroupAttribute>()))
            .Where(entry => entry.Group is not null)
            .OrderBy(entry => entry.Descriptor.Scope == SettingsScope.Household)
            .ThenBy(entry => entry.Group!.Order);

        return [.. groups.Select(entry => BuildSection(entry.Descriptor, entry.Group!))];
    }

    private static SettingsSectionSchema BuildSection(ISettingsDescriptor descriptor, SettingsGroupAttribute group)
    {
        var type = descriptor.ValueType;
        var defaults = JsonSerializer.SerializeToElement(Activator.CreateInstance(type), type, JsonOptions);
        var nullability = new NullabilityInfoContext();

        var settings = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.IsDefined(typeof(SettingAttribute)))
            .OrderBy(property => property.MetadataToken)
            .Select(property => BuildSetting(descriptor.Key, property, defaults, nullability))
            .ToList();

        var scope = descriptor.Scope == SettingsScope.Household ? HouseholdScope : PersonalScope;
        return new SettingsSectionSchema(group.SectionId, descriptor.Key, scope, group.Title, group.Description, settings);
    }

    private static SettingSchema BuildSetting(
        string key,
        PropertyInfo property,
        JsonElement defaults,
        NullabilityInfoContext nullability)
    {
        var setting = property.GetCustomAttribute<SettingAttribute>()!;
        var field = JsonNamingPolicy.CamelCase.ConvertName(property.Name);
        var underlying = Nullable.GetUnderlyingType(property.PropertyType);
        var isNullable = underlying is not null
            || (!property.PropertyType.IsValueType && nullability.Create(property).WriteState == NullabilityState.Nullable);

        var control = BuildControl(property, underlying ?? property.PropertyType, setting, isNullable);
        return new SettingSchema(
            $"{key}.{field}",
            field,
            setting.Title,
            setting.Description,
            setting.Keywords,
            control,
            isNullable,
            defaults.GetProperty(field).Clone());
    }

    private static SettingControlSchema BuildControl(PropertyInfo property, Type valueType, SettingAttribute setting, bool isNullable)
    {
        var kind = setting.Control == SettingControlKind.Inferred ? InferKind(property, valueType) : setting.Control;
        if (kind == SettingControlKind.Range && setting.Step <= 0)
        {
            throw new InvalidOperationException($"Range setting {property.DeclaringType?.Name}.{property.Name} needs a Step.");
        }

        var range = property.GetCustomAttribute<RangeAttribute>();
        var required = property.IsDefined(typeof(RequiredAttribute)) || (valueType.IsValueType && !isNullable);

        return new SettingControlSchema(
            KindName(kind),
            required,
            Min: range is null ? null : ToDouble(range.Minimum),
            Max: range is null || IsUnbounded(range.Maximum) ? null : ToDouble(range.Maximum),
            Step: setting.Step > 0 ? setting.Step : null,
            Unit: setting.Unit,
            MinLabel: setting.MinLabel,
            MaxLabel: setting.MaxLabel,
            MaxLength: property.GetCustomAttribute<MaxLengthAttribute>()?.Length,
            Rows: setting.Rows > 0 ? setting.Rows : null,
            Placeholder: setting.Placeholder,
            EmptyLabel: setting.EmptyLabel);
    }

    private static SettingControlKind InferKind(PropertyInfo property, Type valueType)
    {
        if (valueType == typeof(bool))
        {
            return SettingControlKind.Switch;
        }

        if (valueType == typeof(int) || valueType == typeof(long))
        {
            return SettingControlKind.Number;
        }

        if (valueType == typeof(float) || valueType == typeof(double))
        {
            return SettingControlKind.Range;
        }

        if (valueType == typeof(TimeOnly))
        {
            return SettingControlKind.Time;
        }

        throw new InvalidOperationException(
            $"Setting {property.DeclaringType?.Name}.{property.Name} of type {valueType.Name} must name its Control.");
    }

    private static string KindName(SettingControlKind kind)
    {
        return kind switch
        {
            SettingControlKind.Switch => "switch",
            SettingControlKind.Number => "number",
            SettingControlKind.Range => "range",
            SettingControlKind.Textarea => "textarea",
            SettingControlKind.Time => "time",
            SettingControlKind.TimeZone => "timezone",
            SettingControlKind.Voice => "voice",
            SettingControlKind.Model => "model",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a control kind.")
        };
    }

    private static double ToDouble(object bound) => Convert.ToDouble(bound, CultureInfo.InvariantCulture);

    private static bool IsUnbounded(object bound) => bound is int.MaxValue;
}
