namespace Sentium.Registry.Features.Settings;

/// <summary>
/// How the portal edits a setting. Unset fields don't apply to <see cref="Kind"/>; a missing <see cref="Max"/> on a
/// number means no upper bound.
/// </summary>
internal sealed record SettingControlSchema(
    string Kind,
    bool Required,
    double? Min = null,
    double? Max = null,
    double? Step = null,
    string? Unit = null,
    string? MinLabel = null,
    string? MaxLabel = null,
    int? MaxLength = null,
    int? Rows = null,
    string? Placeholder = null,
    string? EmptyLabel = null);
