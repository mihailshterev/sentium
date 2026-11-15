using Microsoft.AspNetCore.Authorization;

namespace Sentium.Infrastructure.Security;

/// <summary>
/// Restricts the decorated controller or action to paired Devices. It names the Device scheme explicitly, so the
/// portal session cookie never opens a Device endpoint and a Device token never opens anything else.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class AuthorizeDeviceAttribute : AuthorizeAttribute
{
    public AuthorizeDeviceAttribute() : base(Policies.Device)
    {
        AuthenticationSchemes = DeviceAuthenticationDefaults.AuthenticationScheme;
    }
}
