namespace Sentium.Registry.Features.Settings;

/// <summary>
/// One section of the portal's Settings page: a single settings key. <see cref="Scope"/> is <c>personal</c> or
/// <c>household</c>.
/// </summary>
internal sealed record SettingsSectionSchema(
    string Id,
    string Key,
    string Scope,
    string Title,
    string Description,
    IReadOnlyList<SettingSchema> Settings);
