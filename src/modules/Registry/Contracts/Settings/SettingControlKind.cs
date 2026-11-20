namespace Sentium.Registry.Contracts.Settings;

/// <summary>
/// The portal control that edits a setting. <see cref="Inferred"/> picks one from the property type: a switch for
/// <see langword="bool"/>, a number for integers, a range for floating point and a time input for
/// <see cref="TimeOnly"/>. String settings must name their control.
/// </summary>
public enum SettingControlKind
{
    Inferred,
    Switch,
    Number,
    Range,
    Textarea,
    Time,
    TimeZone,
    Voice,
    Model
}
