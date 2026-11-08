using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace Sentium.Tests.Integration.Common;

/// <summary>
/// Authenticates every request as the Sovereign <see cref="TestUserId"/>, or - when the request carries
/// <see cref="UserHeader"/> - as a plain Member with that id, so tests can check per-user isolation.
/// </summary>
public class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string AuthenticationScheme = "TestScheme";
    public const string TestUserId = "11111111-0000-0000-0000-000000000001";
    public const string UserHeader = "X-Test-User";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var member = Request.Headers.TryGetValue(UserHeader, out var header) && Guid.TryParse(header.ToString(), out var memberId)
            ? memberId.ToString()
            : null;

        var userId = member ?? TestUserId;
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, member is null ? "TestUser" : $"member-{member[..8]}"),
            new(ClaimTypes.NameIdentifier, userId),
            new("sub", userId),
            new(ClaimTypes.Role, member is null ? "Sovereign" : "Member")
        };

        var identity = new ClaimsIdentity(claims, AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, AuthenticationScheme);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
