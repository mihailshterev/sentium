using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Sentium.Infrastructure.Security;

namespace Sentium.Identity.Features.Devices;

/// <summary>
/// Authenticates <c>Authorization: Bearer &lt;device token&gt;</c>. The principal carries the owner's user id and the
/// device id and deliberately no role claims: a Device acts for its owner only on the Device endpoints, which name
/// this scheme explicitly. The cookie stays the default scheme, so a plain <c>[Authorize]</c> never runs this handler.
/// </summary>
internal sealed class DeviceAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IDeviceAuthenticator authenticator) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private const string BearerPrefix = "Bearer ";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var token = header[BearerPrefix.Length..].Trim();
        if (token.Length == 0)
        {
            return AuthenticateResult.NoResult();
        }

        var appVersion = DeviceUserAgent.ParseAppVersion(Request.Headers[HeaderNames.UserAgent].ToString());

        var device = await authenticator.AuthenticateAsync(token, appVersion, Context.RequestAborted);
        if (device is null)
        {
            return AuthenticateResult.Fail("Unknown device token.");
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, device.OwnerUserId.ToString()),
                new Claim(DeviceAuthenticationDefaults.DeviceIdClaimType, device.DeviceId.ToString())
            ],
            Scheme.Name);

        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }
}
