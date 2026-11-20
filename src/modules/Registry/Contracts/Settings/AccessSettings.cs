using Sentium.Core.Constants;

namespace Sentium.Registry.Contracts.Settings;

/// <summary>
/// Global settings for who may join the household, managed by Sovereign users and consumed by Identity.
/// </summary>
[SettingsGroup(
    SettingsKeys.Access,
    SettingsScope.Household,
    "access",
    "Access",
    "Who can create an account on this installation.",
    Order = 3)]
public sealed class AccessSettings
{
    /// <summary>
    /// Whether anyone who can reach the portal may create a Member account. Closed by default; Identity always lets
    /// the first account register while no Sovereign exists.
    /// </summary>
    [Setting(
        "Open registration",
        "Lets anyone who can reach the portal create a Member account. Keep it off and open it only while someone joins the household.",
        Keywords = ["register", "sign up", "account", "member", "invite", "users"])]
    public bool RegistrationOpen { get; set; }
}
