namespace Sentium.Registry.Contracts.Settings;

/// <summary>
/// Who a settings group belongs to: <see cref="Personal"/> groups are stored per user and only affect the caller;
/// <see cref="Household"/> groups are stored once, apply to everyone and need a Sovereign to read or change.
/// </summary>
public enum SettingsScope
{
    Personal,
    Household
}
