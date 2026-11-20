namespace Sentium.Registry.Contracts.Settings;

/// <summary>
/// Shows a settings property on the portal's Settings page; properties without it are not listed there. Bounds come
/// from the property's DataAnnotations (<c>[Range]</c>, <c>[MaxLength]</c>, <c>[Required]</c>), which the Registry
/// also enforces on save, and the default from the contract's initializer. Settings are listed in declaration order.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SettingAttribute(string title, string description) : Attribute
{
    public string Title { get; } = title;

    public string Description { get; } = description;

    /// <summary>
    /// Extra search terms beyond the title and description.
    /// </summary>
    public string[] Keywords { get; init; } = [];

    public SettingControlKind Control { get; init; }

    /// <summary>
    /// The unit shown next to a number input, for example <c>s</c>.
    /// </summary>
    public string? Unit { get; init; }

    /// <summary>
    /// The slider step of a range control; required for one.
    /// </summary>
    public double Step { get; init; }

    public string? MinLabel { get; init; }

    public string? MaxLabel { get; init; }

    /// <summary>
    /// The visible rows of a textarea.
    /// </summary>
    public int Rows { get; init; }

    public string? Placeholder { get; init; }

    /// <summary>
    /// Offers an empty choice with this label on a model select, for example "Default model".
    /// </summary>
    public string? EmptyLabel { get; init; }
}
