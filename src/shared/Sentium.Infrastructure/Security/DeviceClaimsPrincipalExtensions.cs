using System.Security.Claims;

namespace Sentium.Infrastructure.Security;

public static class DeviceClaimsPrincipalExtensions
{
    /// <summary>
    /// The id of the paired Device behind <paramref name="principal"/>, or <see langword="null"/> when the request
    /// was not authenticated by the Device scheme.
    /// </summary>
    public static Guid? GetDeviceId(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var value = principal.FindFirstValue(DeviceAuthenticationDefaults.DeviceIdClaimType);
        return Guid.TryParse(value, out var id) ? id : null;
    }
}
