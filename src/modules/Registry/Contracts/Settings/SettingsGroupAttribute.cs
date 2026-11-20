namespace Sentium.Registry.Contracts.Settings;

/// <summary>
/// Makes a settings contract a Registry settings group: the Registry discovers it, stores it under its key and serves
/// it at <c>/registry/settings/{key}</c> and as a section of the portal's Settings page. The section id is the
/// section's URL slug (<c>/settings/{sectionId}</c>); sections are listed personal first, then household, each by
/// <see cref="Order"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class SettingsGroupAttribute(
    string key,
    SettingsScope scope,
    string sectionId,
    string title,
    string description) : Attribute
{
    public string Key { get; } = key;

    public SettingsScope Scope { get; } = scope;

    public string SectionId { get; } = sectionId;

    public string Title { get; } = title;

    public string Description { get; } = description;

    public int Order { get; init; }
}
