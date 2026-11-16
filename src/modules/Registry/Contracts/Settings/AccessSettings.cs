namespace Sentium.Registry.Contracts.Settings;

/// <summary>
/// Global settings for who may join the household, managed by Sovereign users and consumed by Identity.
/// </summary>
public sealed class AccessSettings
{
    /// <summary>
    /// Whether anyone who can reach the portal may create a Member account. Closed by default; Identity always lets
    /// the first account register while no Sovereign exists.
    /// </summary>
    public bool RegistrationOpen { get; set; }
}
