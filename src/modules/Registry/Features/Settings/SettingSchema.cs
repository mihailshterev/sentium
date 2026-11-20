using System.Text.Json;

namespace Sentium.Registry.Features.Settings;

/// <summary>
/// One editable field of a settings key. <see cref="Id"/> is <c>{key}.{field}</c>; <see cref="DefaultValue"/> is the
/// contract's initializer value in wire form, and <see cref="Nullable"/> means <see langword="null"/> is a valid,
/// meaningful value (usually "inherit" or "use the default").
/// </summary>
internal sealed record SettingSchema(
    string Id,
    string Field,
    string Title,
    string Description,
    IReadOnlyList<string> Keywords,
    SettingControlSchema Control,
    bool Nullable,
    JsonElement DefaultValue);
