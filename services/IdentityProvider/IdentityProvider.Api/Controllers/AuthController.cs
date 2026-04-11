using System.Security.Claims;
using IdentityProvider.Core.Entities;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;

namespace IdentityProvider.Api.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController : ControllerBase
{
    [HttpGet("~/connect/authorize")]
    public IActionResult Authorize()
    {
        var request = HttpContext.GetOpenIddictServerRequest()!;

        if (!User.Identity!.IsAuthenticated)
        {
            return Challenge(IdentityConstants.ApplicationScheme);
        }

        var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        identity.AddClaim(OpenIddictConstants.Claims.Subject, User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        identity.AddClaim(OpenIddictConstants.Claims.Email, User.FindFirstValue(ClaimTypes.Email)!);

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(request.GetScopes());

        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpPost("~/connect/token")]
    public IActionResult Exchange()
    {
        var request = HttpContext.GetOpenIddictServerRequest()!;

        if (request.IsClientCredentialsGrantType())
        {
            var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

            identity.AddClaim(OpenIddictConstants.Claims.Subject, request.ClientId!, OpenIddictConstants.Destinations.AccessToken);

            var principal = new ClaimsPrincipal(identity);
            principal.SetScopes(request.GetScopes());

            return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        return BadRequest("Unsupported grant type.");
    }

    [HttpGet("/connect/user-info")]
    public IActionResult UserInfo()
    {
        return Ok(new
        {
            sub = User.FindFirstValue(ClaimTypes.NameIdentifier),
            email = User.FindFirstValue(ClaimTypes.Email)
        });
    }
}

