namespace Sentium.Infrastructure.Security;

public static class DeviceAuthenticationDefaults
{
    public const string AuthenticationScheme = "Device";

    /// <summary>
    /// Claim carrying the id of the paired Device behind a request. The principal's name identifier is the
    /// Device owner's user id, and it never carries role claims.
    /// </summary>
    public const string DeviceIdClaimType = "device_id";
}
