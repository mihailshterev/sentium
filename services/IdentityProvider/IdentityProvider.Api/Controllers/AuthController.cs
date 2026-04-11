using System.Security.Claims;
using IdentityProvider.Application.Abstractions;
using IdentityProvider.Core.Entities;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;

namespace IdentityProvider.Api.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController(
    UserManager<ApplicationUser> userManager,
    IUserClaimsService userClaimsService) : ControllerBase
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

        var subClaim = new Claim(OpenIddictConstants.Claims.Subject, User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        subClaim.SetDestinations(OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken);
        identity.AddClaim(subClaim);

        var emailClaim = new Claim(OpenIddictConstants.Claims.Email, User.FindFirstValue(ClaimTypes.Email)!);
        emailClaim.SetDestinations(OpenIddictConstants.Destinations.AccessToken);
        identity.AddClaim(emailClaim);

        foreach (var role in User.FindAll(ClaimTypes.Role))
        {
            var roleClaim = new Claim(ClaimTypes.Role, role.Value);
            roleClaim.SetDestinations(OpenIddictConstants.Destinations.AccessToken);
            identity.AddClaim(roleClaim);
        }

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(request.GetScopes());

        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpPost("~/connect/token")]
    public async Task<IActionResult> Exchange(CancellationToken ct)
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

        if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
        {
            var result = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            if (!result.Succeeded)
            {
                return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            var subject = result.Principal!.GetClaim(OpenIddictConstants.Claims.Subject);
            if (subject is null)
            {
                return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            var user = await userManager.FindByIdAsync(subject);
            if (user is null)
            {
                return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            var scopes = result.Principal.GetScopes();
            var claims = await userClaimsService.GetClaimsAsync(user.Id, scopes, ct);

            var identity = new ClaimsIdentity(claims, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            principal.SetScopes(scopes);

            foreach (var claim in principal.Claims)
            {
                claim.SetDestinations(GetDestinations(claim));
            }

            return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        return BadRequest("Unsupported grant type.");
    }

    private static IEnumerable<string> GetDestinations(Claim claim)
    {
        switch (claim.Type)
        {
            case ClaimTypes.NameIdentifier:
                yield return OpenIddictConstants.Destinations.AccessToken;
                yield return OpenIddictConstants.Destinations.IdentityToken;
                break;

            case ClaimTypes.Email:
                yield return OpenIddictConstants.Destinations.AccessToken;
                yield return OpenIddictConstants.Destinations.IdentityToken;
                break;

            case ClaimTypes.Name:
                yield return OpenIddictConstants.Destinations.AccessToken;
                yield return OpenIddictConstants.Destinations.IdentityToken;
                break;

            case ClaimTypes.Role:
                yield return OpenIddictConstants.Destinations.AccessToken;
                yield return OpenIddictConstants.Destinations.IdentityToken;
                break;

            default:
                yield return OpenIddictConstants.Destinations.AccessToken;
                break;
        }
    }
}

